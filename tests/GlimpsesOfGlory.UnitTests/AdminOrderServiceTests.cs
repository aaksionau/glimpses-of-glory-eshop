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
    public async Task MarkShipped_UndefinedCarrier_Throws_AndShipsNothing()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Service().MarkShippedAsync(id, "TRACK1", (ShippingCarrier)99, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.NotEqual(OrderStatus.Shipped, order.Status);
        Assert.Null(order.Carrier);
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
    public async Task MarkShipped_PickupOrder_IsRejected()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);

        var result = await Service().MarkShippedAsync(id, null, ShippingCarrier.Usps, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, result);
        Assert.Equal(OrderStatus.New, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task MarkReadyForPickup_MovesToReadyForPickup_StampsItsTimestamp_AndEmailsTheBuyer()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        var emailSender = new RecordingEmailSender();

        var result = await Service(emailSender).MarkReadyForPickupAsync(id, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.Updated, result);
        var order = await LoadAsync(id);
        Assert.Equal(OrderStatus.ReadyForPickup, order.Status);
        Assert.Equal(T1, order.ReadyForPickupAt);
        Assert.Null(order.ShippedAt);
        Assert.Equal(id, Assert.Single(emailSender.ReadyForPickup).OrderId);
        Assert.Empty(emailSender.Shipped);
    }

    [Fact]
    public async Task MarkReadyForPickup_ShippingOrder_IsRejected()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);
        var emailSender = new RecordingEmailSender();

        var result = await Service(emailSender).MarkReadyForPickupAsync(id, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, result);
        Assert.Equal(OrderStatus.New, (await LoadAsync(id)).Status);
        Assert.Empty(emailSender.ReadyForPickup);
    }

    [Fact]
    public async Task PickupOrder_GoesNew_ReadyForPickup_PickedUp()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        var service = Service();

        Assert.Equal(OrderUpdateResult.InvalidTransition, await service.MarkPickedUpAsync(id, CancellationToken.None));
        Assert.Equal(OrderUpdateResult.Updated, await service.MarkReadyForPickupAsync(id, CancellationToken.None));
        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.Updated, await service.MarkPickedUpAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(OrderStatus.PickedUp, order.Status);
        Assert.Equal(T1, order.ReadyForPickupAt);
        Assert.Equal(T2, order.PickedUpAt);
    }

    [Fact]
    public async Task ResendReadyForPickupEmail_OnlyWhileReadyForPickup()
    {
        var ready = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.ReadyForPickup);
        var pickedUp = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.PickedUp);
        var emailSender = new RecordingEmailSender();
        var service = Service(emailSender);

        Assert.Equal(EmailResendResult.Sent, await service.ResendReadyForPickupEmailAsync(ready, CancellationToken.None));
        Assert.Equal(EmailResendResult.NotAllowed, await service.ResendReadyForPickupEmailAsync(pickedUp, CancellationToken.None));
        Assert.Equal(EmailResendResult.NotAllowed, await service.ResendShippedEmailAsync(ready, CancellationToken.None));
        Assert.Equal(ready, Assert.Single(emailSender.ReadyForPickup).OrderId);
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
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, lines: [Line(productId: 1, quantity: 2), Line(isPreorder: true, productId: 2, quantity: 3)]);

        Assert.Equal(OrderUpdateResult.Updated, await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None));

        Assert.Equal(5, store.GetStock(1));
        Assert.Equal(0, store.GetPreordered(2));
    }

    [Fact]
    public async Task MarkCancelled_DoesNotReleaseAgain_WhenAlreadyCancelled()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 5 });
        await store.TryReserveStockAsync(1, 2, CancellationToken.None);
        var id = await AddOrderAsync(FulfillmentMethod.Pickup, lines: [Line(productId: 1, quantity: 2)]);
        await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service(inventoryStore: store).MarkCancelledAsync(id, CancellationToken.None));

        Assert.Equal(5, store.GetStock(1));
    }

    [Fact]
    public async Task GetOrders_FiltersByTab_AndCountsMatch()
    {
        var newCash = await AddOrderAsync(FulfillmentMethod.Shipping);
        var newCard = await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var readyUnpaidCash = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.ReadyForPickup);
        var shippedPaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var pickedUpPaid = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.PickedUp, paidAt: T1);
        var cancelledUnpaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);
        var service = Service();

        Assert.Equal([newCash, newCard, readyUnpaidCash], await IdsAsync(service, AdminOrderTab.NeedsAction));
        Assert.Equal([readyUnpaidCash, shippedPaid, pickedUpPaid], await IdsAsync(service, AdminOrderTab.Shipped));
        Assert.Equal([cancelledUnpaid], await IdsAsync(service, AdminOrderTab.Cancelled));
        Assert.Equal(6, (await IdsAsync(service, AdminOrderTab.All)).Count);

        Assert.Equal(new AdminOrderTabCounts(NeedsAction: 3, Shipped: 3, Cancelled: 1, All: 6), await service.GetOrderTabCountsAsync(AdminOrderFilter.None, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrders_NeedsActionTab_AgreesWithTheRowHighlight()
    {
        await AddOrderAsync(FulfillmentMethod.Shipping);
        await AddOrderAsync(FulfillmentMethod.Pickup);
        await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.ReadyForPickup);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paidAt: T1);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);

        var service = Service();
        var all = (await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 50, CancellationToken.None)).Orders;
        var needsAction = await IdsAsync(service, AdminOrderTab.NeedsAction);

        Assert.Equal(all.Where(o => o.NeedsAction).Select(o => o.Id).Order(), needsAction);
    }

    [Fact]
    public async Task GetOrders_RefundFlag_AgreesWithTheDetailPage()
    {
        var cancelledPaidCard = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled, paymentMethod: PaymentMethod.Card, paidAt: T1);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);
        await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var service = Service();

        var rows = (await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 50, CancellationToken.None)).Orders;

        Assert.Equal([cancelledPaidCard], rows.Where(o => o.NeedsRefund).Select(o => o.Id));
        foreach (var row in rows)
        {
            var detail = await service.GetOrderAsync(row.Id, CancellationToken.None);
            Assert.Equal(row.NeedsRefund, detail!.Actions.NeedsRefund);
        }
    }

    [Fact]
    public async Task GetOrder_GroupsTheStepTimesAndTheLifecycleActions()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, shippedAt: T2, status: OrderStatus.Shipped, paymentMethod: PaymentMethod.Card, paidAt: T1);

        var detail = await Service().GetOrderAsync(id, CancellationToken.None);

        Assert.Equal(T1, detail!.Timestamps.PaidAt);
        Assert.Equal(T2, detail.Timestamps.ShippedAt);
        Assert.Null(detail.Timestamps.CancelledAt);
        Assert.Equal(OrderLifecycle.Actions(OrderFacts.Of(await LoadAsync(id))), detail.Actions);
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
    public async Task UpdateInternalNote_SavesTheTrimmedNote_AndGetOrderExposesIt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);

        var result = await Service().UpdateInternalNoteAsync(id, "  Customer called, collecting Friday \n", CancellationToken.None);

        Assert.Equal(OrderUpdateResult.Updated, result);
        var detail = await Service().GetOrderAsync(id, CancellationToken.None);
        Assert.Equal("Customer called, collecting Friday", detail!.InternalNote);
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
    [InlineData(typeof(OrderReadyForPickupView))]
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

    private static void AssertUnchanged(Order before, Order after)
    {
        Assert.Equal(
            (before.Status, before.TrackingNumber, before.CreatedAt, before.PaidAt, before.ShippedAt, before.ReadyForPickupAt, before.PickedUpAt, before.CancelledAt),
            (after.Status, after.TrackingNumber, after.CreatedAt, after.PaidAt, after.ShippedAt, after.ReadyForPickupAt, after.PickedUpAt, after.CancelledAt));
    }

    private static async Task<List<int>> IdsAsync(AdminOrderService service, AdminOrderTab tab, AdminOrderFilter? filter = null)
    {
        var page = await service.GetOrdersAsync(tab, filter ?? AdminOrderFilter.None, 1, 100, CancellationToken.None);
        // Same CreatedAt for most seeded orders, so compare by id to stay order-independent.
        return page.Orders.Select(o => o.Id).Order().ToList();
    }

    private static OrderLine Line(bool isPreorder = false, int productId = 1, int quantity = 1) => new()
    {
        ProductId = productId,
        ProductName = "Print",
        UnitPrice = 10m,
        Quantity = quantity,
        IsPreorder = isPreorder,
    };

    private AppDbContext CreateDb() => database.CreateDb();

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
        string? internalNote = null)
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
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Order> LoadAsync(int id)
    {
        await using var db = CreateDb();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
