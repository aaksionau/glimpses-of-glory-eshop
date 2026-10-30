using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.UnitTests;

// Runs AdminOrderService against an in-memory SQLite database, since its status changes are
// ExecuteUpdate calls that need a relational provider.
public sealed class AdminOrderServiceTests : IDisposable
{
    private static readonly DateTimeOffset T1 = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase database = new();
    private readonly ManualTimeProvider clock = new(T1);

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task MarkShipped_StampsShippedAt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        var result = await Service().MarkShippedAsync(id, "TRACK1", ShippingCarrier.Usps, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.Updated, result);
        var order = await LoadAsync(id);
        Assert.Equal(T1, order.ShippedAt);
        Assert.Null(order.PickedUpAt);
        Assert.Null(order.CancelledAt);
    }

    [Fact]
    public async Task MarkShipped_StoresTheCarrier_AndEmailsTheTrackingLink()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);
        var emailSender = new RecordingEmailSender();

        await Service(emailSender).MarkShippedAsync(id, " 9400111899223 ", ShippingCarrier.Usps, CancellationToken.None);

        var order = await LoadAsync(id);
        Assert.Equal(ShippingCarrier.Usps, order.Carrier);
        Assert.Equal("9400111899223", order.TrackingNumber);
        var sent = Assert.Single(emailSender.Shipped);
        Assert.Equal("https://tools.usps.com/go/TrackConfirmAction?tLabels=9400111899223", sent.TrackingUrl);
    }

    [Fact]
    public async Task MarkShipped_OtherCarrier_EmailsThePlainTrackingNumber()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);
        var emailSender = new RecordingEmailSender();

        await Service(emailSender).MarkShippedAsync(id, "ABC123", ShippingCarrier.Other, CancellationToken.None);

        Assert.Equal(ShippingCarrier.Other, (await LoadAsync(id)).Carrier);
        var sent = Assert.Single(emailSender.Shipped);
        Assert.Equal("ABC123", sent.TrackingNumber);
        Assert.Null(sent.TrackingUrl);
    }

    [Fact]
    public async Task MarkShipped_WithoutTrackingNumber_RecordsNoCarrier()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        await Service().MarkShippedAsync(id, "  ", ShippingCarrier.Usps, CancellationToken.None);

        var order = await LoadAsync(id);
        Assert.Null(order.TrackingNumber);
        Assert.Null(order.Carrier);
    }

    [Fact]
    public async Task MarkShipped_UndefinedCarrier_Throws_AndShipsNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Service().MarkShippedAsync(id, "TRACK1", (ShippingCarrier)99, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.NotEqual(OrderStatus.Shipped, order.Status);
        Assert.Null(order.Carrier);
    }

    [Theory]
    [InlineData(ShippingCarrier.Usps, "https://tools.usps.com/go/TrackConfirmAction?tLabels=TRACK1")]
    [InlineData(ShippingCarrier.Other, null)]
    [InlineData(null, null)]
    public async Task GetOrder_LinksTheTrackingNumberOnlyForKnownCarriers(ShippingCarrier? carrier, string? expectedUrl)
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, trackingNumber: "TRACK1", carrier: carrier);

        var order = await Service().GetOrderAsync(id, CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(carrier, order.Carrier);
        Assert.Equal(expectedUrl, order.TrackingUrl);
    }

    [Fact]
    public async Task MarkShipped_RepeatCall_DoesNotMoveShippedAt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);
        await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);

        clock.Now = T2;
        var result = await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, result);
        Assert.Equal(T1, (await LoadAsync(id)).ShippedAt);
    }

    [Fact]
    public async Task MarkShipped_KeepsAnAlreadyRecordedShippedAt()
    {
        // The update coalesces, so even a call that gets past the status guard can't move the stamp.
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, shippedAt: T1);
        clock.Now = T2;

        await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);

        Assert.Equal(T1, (await LoadAsync(id)).ShippedAt);
    }

    [Fact]
    public async Task MarkPickedUp_StampsPickedUpAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);

        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkPickedUpAsync(id, CancellationToken.None));
        clock.Now = T2.AddDays(1);
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkPickedUpAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(T1, order.ShippedAt);
        Assert.Equal(T2, order.PickedUpAt);
    }

    [Fact]
    public async Task MarkPaid_StampsPaidAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);

        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkPaidAsync(id, CancellationToken.None));
        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkPaidAsync(id, CancellationToken.None));

        Assert.Equal(T1, (await LoadAsync(id)).PaidAt);
    }

    [Fact]
    public async Task MarkCancelled_StampsCancelledAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkCancelledAsync(id, CancellationToken.None));
        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkCancelledAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(T1, order.CancelledAt);
        Assert.Null(order.ShippedAt);
    }

    [Fact]
    public async Task MarkCancelled_ReleasesEachLine()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 5, [2] = 0 });
        await store.TryReserveStockAsync(1, 2, CancellationToken.None);
        await store.ReservePreorderAsync(2, 3, CancellationToken.None);
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, lines: [LineOf(1, 2, isPreorder: false), LineOf(2, 3, isPreorder: true)]);

        Assert.Equal(OrderUpdateResult.Updated, await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None));

        Assert.Equal(5, store.GetStock(1));
        Assert.Equal(0, store.GetPreordered(2));
    }

    [Fact]
    public async Task MarkCancelled_DoesNotReleaseAgain_WhenAlreadyCancelled()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 5 });
        await store.TryReserveStockAsync(1, 2, CancellationToken.None);
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, lines: [LineOf(1, 2, isPreorder: false)]);
        await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None));

        Assert.Equal(5, store.GetStock(1));
    }

    [Fact]
    public async Task GetOrder_ExposesTheStepTimestamps()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);
        clock.Now = T2;
        await Service().MarkPickedUpAsync(id, CancellationToken.None);

        var detail = await Service().GetOrderAsync(id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(T1, detail.ShippedAt);
        Assert.Equal(T2, detail.PickedUpAt);
        Assert.Null(detail.PaidAt);
        Assert.Null(detail.CancelledAt);
    }

    [Fact]
    public async Task GetOrders_FiltersByTab_AndCountsMatch()
    {
        var newCash = await AddOrderAsync(FulfillmentMethod.Shipping);
        var newCard = await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var shippedUnpaidCash = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped);
        var shippedPaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var pickedUpPaid = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.PickedUp, paidAt: T1);
        var cancelledUnpaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);
        var service = Service();

        Assert.Equal([newCash, newCard, shippedUnpaidCash], await IdsAsync(service, AdminOrderTab.NeedsAction));
        Assert.Equal([shippedUnpaidCash, shippedPaid, pickedUpPaid], await IdsAsync(service, AdminOrderTab.Shipped));
        Assert.Equal([cancelledUnpaid], await IdsAsync(service, AdminOrderTab.Cancelled));
        Assert.Equal(6, (await IdsAsync(service, AdminOrderTab.All)).Count);

        Assert.Equal(new AdminOrderTabCounts(NeedsAction: 3, Shipped: 3, Cancelled: 1, All: 6), await service.GetOrderTabCountsAsync(AdminOrderFilter.None, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrders_NeedsActionTab_AgreesWithTheRowHighlight()
    {
        await AddOrderAsync(FulfillmentMethod.Shipping);
        await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paidAt: T1);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);

        var service = Service();
        var all = (await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 50, CancellationToken.None)).Orders;
        var needsAction = await IdsAsync(service, AdminOrderTab.NeedsAction);

        Assert.Equal(all.Where(o => o.NeedsAction).Select(o => o.Id).Order(), needsAction);
    }

    [Fact]
    public async Task GetOrders_ListsNewestFirst()
    {
        var older = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: T1);
        var newer = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: T2);

        var orders = await Service().GetOrdersAsync(AdminOrderTab.NeedsAction, AdminOrderFilter.None, 1, 25, CancellationToken.None);

        Assert.Equal([newer, older], orders.Orders.Select(o => o.Id));
    }

    [Fact]
    public async Task GetOrderTabCounts_WithNoOrders_IsAllZero()
    {
        Assert.Equal(new AdminOrderTabCounts(0, 0, 0, 0), await Service().GetOrderTabCountsAsync(AdminOrderFilter.None, CancellationToken.None));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("#42")]
    [InlineData(" # 42 ")]
    public async Task GetOrders_SearchByOrderNumber_FindsThatOrder(string search)
    {
        for (var i = 0; i < 42; i++)
        {
            await AddOrderAsync(FulfillmentMethod.Shipping);
        }

        var ids = await IdsAsync(Service(), AdminOrderTab.All, new AdminOrderFilter(search));

        Assert.Equal([42], ids);
    }

    [Fact]
    public async Task GetOrders_HashSearch_MatchesOrderNumbersOnly()
    {
        var first = await AddOrderAsync(FulfillmentMethod.Shipping, email: "first@example.com");
        var second = await AddOrderAsync(FulfillmentMethod.Shipping, email: "buyer1@example.com");
        var service = Service();

        Assert.Equal([first], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("#1")));
        Assert.Empty(await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("#buyer")));
        // Without the "#", a number also matches emails that contain it.
        Assert.Equal([first, second], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("1")));
    }

    [Fact]
    public async Task GetOrders_SearchMatchesPartOfTheEmailOrName_IgnoringCase()
    {
        var alice = await AddOrderAsync(FulfillmentMethod.Shipping, email: "alice.walker@example.com", customerName: "Alice Walker");
        var bob = await AddOrderAsync(FulfillmentMethod.Shipping, email: "bob@shop.test", customerName: "Robert Stone");
        await AddOrderAsync(FulfillmentMethod.Shipping, email: "carol@example.com", customerName: "Carol King");
        var service = Service();

        Assert.Equal([alice], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("WALKER@")));
        Assert.Equal([bob], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("shop.te")));
        Assert.Equal([bob], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("rob")));
        Assert.Equal([alice], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("ice wal")));
        Assert.Equal(3, (await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("   "))).Count);
        Assert.Empty(await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter("nobody")));
    }

    [Fact]
    public async Task GetOrders_SearchCombinesWithTheTab_AndTheCountsFollowIt()
    {
        var newJane = await AddOrderAsync(FulfillmentMethod.Shipping, customerName: "Jane Doe");
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled, customerName: "Jane Doe");
        await AddOrderAsync(FulfillmentMethod.Shipping, customerName: "John Roe");
        var service = Service();
        var filter = new AdminOrderFilter("jane");

        Assert.Equal([newJane], await IdsAsync(service, AdminOrderTab.NeedsAction, filter));
        Assert.Equal(new AdminOrderTabCounts(NeedsAction: 1, Shipped: 0, Cancelled: 1, All: 2), await service.GetOrderTabCountsAsync(filter, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrders_PagesNewestFirst_WithTheTotalCount()
    {
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: T1.AddHours(i)));
        }
        ids.Reverse();
        var service = Service();

        var first = await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 2, CancellationToken.None);
        var last = await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 3, 2, CancellationToken.None);

        Assert.Equal(ids[..2], first.Orders.Select(o => o.Id));
        Assert.Equal((1, 5, 3, false, true), (first.Page, first.TotalCount, first.TotalPages, first.HasPrevious, first.HasNext));
        Assert.Equal(ids[4..], last.Orders.Select(o => o.Id));
        Assert.Equal((3, true, false, 5, 5), (last.Page, last.HasPrevious, last.HasNext, last.FirstItem, last.LastItem));
    }

    [Fact]
    public async Task GetOrders_PagesThroughOrdersPlacedAtTheSameTime_WithoutRepeatsOrGaps()
    {
        for (var i = 0; i < 5; i++)
        {
            await AddOrderAsync(FulfillmentMethod.Shipping);
        }
        var service = Service();

        var seen = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange((await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, page, 2, CancellationToken.None)).Orders.Select(o => o.Id));
        }

        Assert.Equal([5, 4, 3, 2, 1], seen);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(9, 2)]
    public async Task GetOrders_OutOfRangePage_ReturnsTheNearestPage(int requested, int expected)
    {
        for (var i = 0; i < 3; i++)
        {
            await AddOrderAsync(FulfillmentMethod.Shipping);
        }

        var page = await Service().GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, requested, 2, CancellationToken.None);

        Assert.Equal(expected, page.Page);
        Assert.NotEmpty(page.Orders);
    }

    [Fact]
    public async Task GetOrders_WithNoMatches_IsOneEmptyPage()
    {
        var page = await Service().GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 4, 25, CancellationToken.None);

        Assert.Equal((1, 1, 0, false, false), (page.Page, page.TotalPages, page.TotalCount, page.HasPrevious, page.HasNext));
        Assert.Empty(page.Orders);
    }

    [Fact]
    public async Task GetOrders_PreorderFilter_KeepsOrdersWithPreorderLines_AndCombinesWithSearchAndTab()
    {
        var preorderJane = await AddOrderAsync(FulfillmentMethod.Shipping, customerName: "Jane Doe", lines: [Line(isPreorder: true)]);
        await AddOrderAsync(FulfillmentMethod.Shipping, customerName: "Jane Doe", lines: [Line()]);
        var preorderJohn = await AddOrderAsync(FulfillmentMethod.Shipping, customerName: "John Roe", lines: [Line(), Line(isPreorder: true)]);
        var shippedPreorderJane = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paidAt: T1, customerName: "Jane Doe", lines: [Line(isPreorder: true)]);
        var service = Service();
        var preorders = new AdminOrderFilter(PreorderOnly: true);
        var janesPreorders = new AdminOrderFilter("jane", PreorderOnly: true);

        Assert.Equal([preorderJane, preorderJohn, shippedPreorderJane], await IdsAsync(service, AdminOrderTab.All, preorders));
        Assert.Equal([preorderJane, shippedPreorderJane], await IdsAsync(service, AdminOrderTab.All, janesPreorders));
        Assert.Equal([preorderJane], await IdsAsync(service, AdminOrderTab.NeedsAction, janesPreorders));
        Assert.Equal(new AdminOrderTabCounts(NeedsAction: 1, Shipped: 1, Cancelled: 0, All: 2), await service.GetOrderTabCountsAsync(janesPreorders, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrders_ToFulfillAndUnpaidCashFilters_NarrowTheList()
    {
        var newCash = await AddOrderAsync(FulfillmentMethod.Pickup);
        var newCard = await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var readyUnpaidCash = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped);
        await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.PickedUp, paidAt: T1);
        await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Cancelled);
        await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, status: OrderStatus.Shipped);
        var service = Service();
        var toFulfill = new AdminOrderFilter(ToFulfillOnly: true);
        var unpaidCash = new AdminOrderFilter(UnpaidCashOnly: true);

        Assert.Equal([newCash, newCard], await IdsAsync(service, AdminOrderTab.All, toFulfill));
        Assert.Equal([newCash, readyUnpaidCash], await IdsAsync(service, AdminOrderTab.All, unpaidCash));
        Assert.Equal([newCash], await IdsAsync(service, AdminOrderTab.All, new AdminOrderFilter(ToFulfillOnly: true, UnpaidCashOnly: true)));
        // Both are subsets of the default tab, so the dashboard links can land there.
        Assert.Equal(2, (await service.GetOrderTabCountsAsync(toFulfill, CancellationToken.None)).NeedsAction);
        Assert.Equal(2, (await service.GetOrderTabCountsAsync(unpaidCash, CancellationToken.None)).NeedsAction);
    }

    [Fact]
    public async Task GetOrders_ShowsTheEarliestExpectedAvailabilityOfThePreorderLines()
    {
        var mixed = await AddOrderAsync(FulfillmentMethod.Shipping, lines:
        [
            Line(isPreorder: true, expectedAvailability: new DateOnly(2026, 12, 1)),
            Line(isPreorder: true, expectedAvailability: new DateOnly(2026, 11, 15)),
            Line(isPreorder: true),
            Line(expectedAvailability: new DateOnly(2026, 9, 1)),
        ]);
        var undated = await AddOrderAsync(FulfillmentMethod.Shipping, lines: [Line(isPreorder: true)]);
        var inStock = await AddOrderAsync(FulfillmentMethod.Shipping, lines: [Line()]);

        var orders = (await Service().GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 25, CancellationToken.None)).Orders.ToDictionary(o => o.Id);

        Assert.Equal(new DateOnly(2026, 11, 15), orders[mixed].EarliestExpectedAvailability);
        Assert.True(orders[undated].HasPreorderLines);
        Assert.Null(orders[undated].EarliestExpectedAvailability);
        Assert.False(orders[inStock].HasPreorderLines);
        Assert.Null(orders[inStock].EarliestExpectedAvailability);
    }

    [Fact]
    public async Task UpdateInternalNote_SavesTheTrimmedNote_AndGetOrderExposesIt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);

        var result = await Service().UpdateInternalNoteAsync(id, "  Customer called, collecting Friday \n", CancellationToken.None);

        Assert.Equal(OrderUpdateResult.Updated, result);
        var detail = await Service().GetOrderAsync(id, CancellationToken.None);
        Assert.Equal("Customer called, collecting Friday", detail!.InternalNote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateInternalNote_WithABlankNote_ClearsIt(string? note)
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, internalNote: "Old note");

        await Service().UpdateInternalNoteAsync(id, note, CancellationToken.None);

        Assert.Null((await LoadAsync(id)).InternalNote);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateInternalNote_IsAllowedInAnyStatus(OrderStatus status)
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: status);

        Assert.Equal(OrderUpdateResult.Updated, await Service().UpdateInternalNoteAsync(id, "Refund agreed by phone", CancellationToken.None));
        var order = await LoadAsync(id);
        Assert.Equal("Refund agreed by phone", order.InternalNote);
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public async Task UpdateInternalNote_StoresLineEndingsAsLf()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        await Service().UpdateInternalNoteAsync(id, "Line one\r\nLine two", CancellationToken.None);

        Assert.Equal("Line one\nLine two", (await LoadAsync(id)).InternalNote);
    }

    [Fact]
    public async Task UpdateInternalNote_UnknownOrder_IsNotFound()
    {
        Assert.Equal(OrderUpdateResult.NotFound, await Service().UpdateInternalNoteAsync(999, "Note", CancellationToken.None));
    }

    [Fact]
    public async Task UpdateInternalNote_TooLong_Throws_AndSavesNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service().UpdateInternalNoteAsync(id, new string('x', OrderNotePolicy.MaxLength + 1), CancellationToken.None));
        Assert.Null((await LoadAsync(id)).InternalNote);
    }

    [Fact]
    public async Task GetOrders_FlagsOrdersThatHaveAnInternalNote()
    {
        var withNote = await AddOrderAsync(FulfillmentMethod.Shipping, internalNote: "Collecting Friday");
        var withoutNote = await AddOrderAsync(FulfillmentMethod.Shipping);

        var page = await Service().GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 100, CancellationToken.None);

        var orders = page.Orders.ToDictionary(o => o.Id);
        Assert.True(orders[withNote].HasInternalNote);
        Assert.False(orders[withoutNote].HasInternalNote);
    }

    [Fact]
    public async Task MarkShipped_NeverPutsTheInternalNoteInTheBuyersEmail()
    {
        const string note = "Customer was rude on the phone";
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, internalNote: note);
        var emailSender = new RecordingEmailSender();

        await Service(emailSender).MarkShippedAsync(id, "TRACK1", ShippingCarrier.Usps, CancellationToken.None);

        var sent = Assert.Single(emailSender.Shipped);
        var html = await RazorViewToStringRendererTests.CreateRenderer().RenderAsync("~/Emails/OrderShipped.cshtml", sent);
        Assert.Contains("TRACK1", html);
        Assert.DoesNotContain(note, html);
    }

    [Theory]
    [InlineData(typeof(OrderConfirmationView))]
    [InlineData(typeof(OrderShippedView))]
    public void CustomerFacingOrderViews_DoNotCarryTheInternalNote(Type customerView)
    {
        // These views feed the customer emails and the customer order pages; the note must stay admin-only.
        Assert.DoesNotContain(customerView.GetProperties(), p => p.Name.Contains("Note", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResendConfirmation_SendsTheConfirmation_AndChangesNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paidAt: T1, shippedAt: T1, lines: [Line()]);
        var before = await LoadAsync(id);
        var emailSender = new RecordingEmailSender();
        clock.Now = T2;

        var result = await Service(emailSender).ResendConfirmationAsync(id, CancellationToken.None);

        Assert.Equal(EmailResendResult.Sent, result);
        var sent = Assert.Single(emailSender.Confirmations);
        Assert.Equal(id, sent.OrderId);
        Assert.Equal("shopper@example.com", sent.Address.Email);
        Assert.Single(sent.Lines);
        Assert.Empty(emailSender.Shipped);
        AssertUnchanged(before, await LoadAsync(id));
    }

    [Fact]
    public async Task ResendShippedEmail_SendsTheShippedEmail_AndChangesNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped, shippedAt: T1, trackingNumber: "TRACK1");
        var before = await LoadAsync(id);
        var emailSender = new RecordingEmailSender();
        clock.Now = T2;

        var result = await Service(emailSender).ResendShippedEmailAsync(id, CancellationToken.None);

        Assert.Equal(EmailResendResult.Sent, result);
        var sent = Assert.Single(emailSender.Shipped);
        Assert.Equal(FulfillmentMethod.Pickup, sent.FulfillmentMethod);
        Assert.Equal("TRACK1", sent.TrackingNumber);
        Assert.Empty(emailSender.Confirmations);
        AssertUnchanged(before, await LoadAsync(id));
    }

    [Fact]
    public async Task ResendConfirmation_CancelledOrder_IsNotAllowed_AndSendsNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);
        var emailSender = new RecordingEmailSender();

        Assert.Equal(EmailResendResult.NotAllowed, await Service(emailSender).ResendConfirmationAsync(id, CancellationToken.None));
        Assert.Empty(emailSender.Confirmations);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task ResendShippedEmail_BeforeShippedOrAfterIt_IsNotAllowed_AndSendsNothing(OrderStatus status)
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, status: status);
        var emailSender = new RecordingEmailSender();

        Assert.Equal(EmailResendResult.NotAllowed, await Service(emailSender).ResendShippedEmailAsync(id, CancellationToken.None));
        Assert.Empty(emailSender.Shipped);
    }

    [Fact]
    public async Task Resend_UnknownOrder_IsNotFound()
    {
        Assert.Equal(EmailResendResult.NotFound, await Service().ResendConfirmationAsync(999, CancellationToken.None));
        Assert.Equal(EmailResendResult.NotFound, await Service().ResendShippedEmailAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task Resend_WhenTheEmailFails_ReportsFailed_AndChangesNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, shippedAt: T1);
        var before = await LoadAsync(id);
        var service = Service(new RecordingEmailSender { Fail = true });

        Assert.Equal(EmailResendResult.Failed, await service.ResendConfirmationAsync(id, CancellationToken.None));
        Assert.Equal(EmailResendResult.Failed, await service.ResendShippedEmailAsync(id, CancellationToken.None));
        AssertUnchanged(before, await LoadAsync(id));
    }

    [Fact]
    public async Task Resend_WhenTheEmailTimesOut_ReportsFailed()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, shippedAt: T1);
        var service = Service(new RecordingEmailSender { TimeOut = true });

        Assert.Equal(EmailResendResult.Failed, await service.ResendConfirmationAsync(id, CancellationToken.None));
        Assert.Equal(EmailResendResult.Failed, await service.ResendShippedEmailAsync(id, CancellationToken.None));
    }

    [Theory]
    [InlineData(OrderStatus.New, true, false)]
    [InlineData(OrderStatus.Shipped, true, true)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    public async Task GetOrder_ExposesWhichEmailsCanBeResent(OrderStatus status, bool confirmation, bool shipped)
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, status: status);

        var detail = await Service().GetOrderAsync(id, CancellationToken.None);

        Assert.Equal((confirmation, shipped), (detail!.CanResendConfirmation, detail.CanResendShippedEmail));
    }

    private static void AssertUnchanged(Order before, Order after)
    {
        Assert.Equal(
            (before.Status, before.TrackingNumber, before.CreatedAt, before.PaidAt, before.ShippedAt, before.PickedUpAt, before.CancelledAt),
            (after.Status, after.TrackingNumber, after.CreatedAt, after.PaidAt, after.ShippedAt, after.PickedUpAt, after.CancelledAt));
    }

    private static async Task<List<int>> IdsAsync(AdminOrderService service, AdminOrderTab tab, AdminOrderFilter? filter = null)
    {
        var page = await service.GetOrdersAsync(tab, filter ?? AdminOrderFilter.None, 1, 100, CancellationToken.None);
        // Same CreatedAt for most seeded orders, so compare by id to stay order-independent.
        return page.Orders.Select(o => o.Id).Order().ToList();
    }

    private static OrderLine Line(bool isPreorder = false, DateOnly? expectedAvailability = null) => new()
    {
        ProductId = 1,
        ProductName = "Print",
        UnitPrice = 10m,
        Quantity = 1,
        IsPreorder = isPreorder,
        ExpectedAvailabilityDate = expectedAvailability,
    };

    private AppDbContext CreateDb() => database.CreateDb();

    [Fact]
    public async Task StreamOrdersForExportAsync_ReturnsTheWholeUtcDayRange_OldestFirst()
    {
        await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero));
        var lastMoment = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero));
        var first = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        var rows = await Service().StreamOrdersForExportAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None).ToListAsync();

        Assert.Equal([first, lastMoment], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task StreamOrdersForExportAsync_CarriesTheBookkeepingColumns()
    {
        var paidAt = new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped, paymentMethod: PaymentMethod.Card, paidAt: paidAt,
            createdAt: createdAt, email: "zoe@example.com", customerName: "Zoë", subtotal: 42.5m, shippingCost: 7m);

        var row = Assert.Single(await Service().StreamOrdersForExportAsync(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 2), CancellationToken.None).ToListAsync());

        Assert.Equal(new AdminOrderExportRow(id, createdAt, "Zoë", "zoe@example.com", FulfillmentMethod.Pickup, OrderStatus.Shipped, PaymentMethod.Card, paidAt, 42.5m, 7m), row);
        Assert.Equal(49.5m, row.Total);
    }

    [Fact]
    public void StreamOrdersForExportAsync_BackwardsRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => Service().StreamOrdersForExportAsync(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1), CancellationToken.None));
    }

    private AdminOrderService Service(IEmailSender? emailSender = null, FakeInventoryStore? inventoryStore = null) =>
        new(CreateDb(), new OrderLineInventory(inventoryStore ?? new FakeInventoryStore(new Dictionary<int, int>())), new FakePaymentGateway(), emailSender ?? new NullEmailSender(), clock, NullLogger<AdminOrderService>.Instance);

    private async Task<int> AddOrderAsync(
        FulfillmentMethod fulfillmentMethod,
        DateTimeOffset? shippedAt = null,
        OrderStatus status = OrderStatus.New,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        DateTimeOffset? paidAt = null,
        DateTimeOffset? createdAt = null,
        string email = "shopper@example.com",
        string customerName = "Jane Shopper",
        List<OrderLine>? lines = null,
        string? internalNote = null,
        string? trackingNumber = null,
        ShippingCarrier? carrier = null,
        decimal subtotal = 0m,
        decimal shippingCost = 0m)
    {
        await using var db = CreateDb();
        var order = new Order
        {
            Email = email,
            FulfillmentMethod = fulfillmentMethod,
            ShippingAddress = new ShippingAddress { FullName = customerName },
            Lines = lines ?? [],
            Status = status,
            PaymentMethod = paymentMethod,
            PaidAt = paidAt,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
            ShippedAt = shippedAt,
            InternalNote = internalNote,
            TrackingNumber = trackingNumber,
            Carrier = carrier,
            Subtotal = subtotal,
            ShippingCost = shippingCost,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static OrderLine LineOf(int productId, int quantity, bool isPreorder) => new()
    {
        ProductId = productId,
        ProductName = $"Product {productId}",
        UnitPrice = 10m,
        Quantity = quantity,
        IsPreorder = isPreorder,
    };

    private async Task<Order> LoadAsync(int id)
    {
        await using var db = CreateDb();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<OrderShippedView> Shipped { get; } = [];

        public List<OrderConfirmationView> Confirmations { get; } = [];

        // Simulates the email provider rejecting the send.
        public bool Fail { get; init; }

        // Simulates an HttpClient timeout, which surfaces as a TaskCanceledException.
        public bool TimeOut { get; init; }

        public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Record(Confirmations, order);

        public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken) => Record(Shipped, order);

        private Task Record<T>(List<T> sent, T email)
        {
            if (Fail)
            {
                throw new HttpRequestException("Resend rejected email (500)");
            }
            if (TimeOut)
            {
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
            }

            sent.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
