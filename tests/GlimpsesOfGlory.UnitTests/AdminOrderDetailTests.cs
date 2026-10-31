using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Admin.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace GlimpsesOfGlory.UnitTests;

public class AdminOrderDetailTests
{
    private static AdminOrderDetail Order(
        OrderStatus status = OrderStatus.New,
        FulfillmentMethod fulfillmentMethod = FulfillmentMethod.Shipping,
        PaymentMethod paymentMethod = PaymentMethod.Card,
        bool isPaid = true,
        IReadOnlyList<OrderConfirmationLine>? lines = null) =>
        new(
            Id: 42,
            FulfillmentMethod: fulfillmentMethod,
            PaymentMethod: paymentMethod,
            IsPaid: isPaid,
            PaymentDashboardUrl: null,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: lines ?? [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Subtotal: 25.00m,
            ShippingCost: 5.00m,
            Total: 30.00m,
            Status: status,
            TrackingNumber: null,
            Carrier: null,
            TrackingUrl: null,
            InternalNote: null,
            CreatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            PaidAt: isPaid ? new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero) : null,
            ShippedAt: null,
            ReadyForPickupAt: null,
            PickedUpAt: null,
            CancelledAt: null,
            CanMarkShipped: status == OrderStatus.New && fulfillmentMethod == FulfillmentMethod.Shipping,
            CanMarkReadyForPickup: status == OrderStatus.New && fulfillmentMethod == FulfillmentMethod.Pickup,
            CanMarkPickedUp: status == OrderStatus.ReadyForPickup,
            CanMarkPaid: !isPaid && status != OrderStatus.Cancelled,
            CanMarkCancelled: status == OrderStatus.New,
            NeedsRefund: status == OrderStatus.Cancelled && paymentMethod == PaymentMethod.Card && isPaid,
            CanResendConfirmation: status != OrderStatus.Cancelled,
            CanResendShippedEmail: status == OrderStatus.Shipped,
            CanResendReadyForPickupEmail: status == OrderStatus.ReadyForPickup);

    [Fact]
    public void Primary_NewUnpaidCashOrder_IsFulfilment_WithMarkPaidSecondary()
    {
        var order = Order(fulfillmentMethod: FulfillmentMethod.Pickup, paymentMethod: PaymentMethod.Cash, isPaid: false);

        Assert.Equal(OrderAction.MarkReadyForPickup, OrderActions.Primary(order));
        Assert.Equal([OrderAction.MarkPaid], OrderActions.Secondary(order));
    }

    [Fact]
    public void Primary_NewShippingOrder_IsMarkShipped()
    {
        Assert.Equal(OrderAction.MarkShipped, OrderActions.Primary(Order()));
        Assert.Empty(OrderActions.Secondary(Order()));
    }

    [Fact]
    public void Primary_ReadyForPickupOrder_IsMarkPickedUp()
    {
        var order = Order(status: OrderStatus.ReadyForPickup, fulfillmentMethod: FulfillmentMethod.Pickup);

        Assert.Equal(OrderAction.MarkPickedUp, OrderActions.Primary(order));
    }

    [Fact]
    public void Primary_PickedUpButUnpaid_IsMarkPaid()
    {
        var order = Order(status: OrderStatus.PickedUp, fulfillmentMethod: FulfillmentMethod.Pickup, paymentMethod: PaymentMethod.Cash, isPaid: false);

        Assert.Equal(OrderAction.MarkPaid, OrderActions.Primary(order));
        Assert.Empty(OrderActions.Secondary(order));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void Primary_NothingLeftToDo_IsNull(OrderStatus status)
    {
        Assert.Null(OrderActions.Primary(Order(status: status)));
    }

    [Fact]
    public void Timeline_ShippingOrder_EndsAtShipped()
    {
        var steps = OrderTimeline.Steps(Order(status: OrderStatus.Shipped));

        Assert.Equal(["Placed", "Paid", "Shipped"], steps.Select(s => s.Label));
        Assert.All(steps, s => Assert.True(s.IsComplete));
    }

    [Fact]
    public void Timeline_PickupOrder_GoesThroughReadyForPickup_ToPickedUp()
    {
        var steps = OrderTimeline.Steps(Order(status: OrderStatus.ReadyForPickup, fulfillmentMethod: FulfillmentMethod.Pickup));

        Assert.Equal(["Placed", "Paid", "Ready for pickup", "Picked up"], steps.Select(s => s.Label));
        Assert.Equal([true, true, true, false], steps.Select(s => s.IsComplete));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, "Shipped")]
    [InlineData(OrderStatus.ReadyForPickup, "Ready for pickup")]
    [InlineData(OrderStatus.PickedUp, "Picked up")]
    public void StatusBadge_ReadsTheStatusDirectly(OrderStatus status, string label)
    {
        Assert.Equal(label, OrderBadges.Status(status).Text);
    }

    [Fact]
    public void EveryOrderAction_HasAMatchingPageHandler()
    {
        // _OrderStatus posts to ?handler={action}, so each enum member must name an OnPost{Action}Async handler.
        foreach (var action in Enum.GetValues<OrderAction>())
        {
            Assert.NotNull(typeof(DetailsModel).GetMethod($"OnPost{action}Async"));
        }
    }

    [Fact]
    public async Task MarkShipped_WithInvalidTrackingNumber_SavesNothing()
    {
        var service = new FakeAdminOrderService(Order()) { AfterUpdate = Order(status: OrderStatus.Shipped) };
        var model = CreateModel(service);
        model.ModelState.AddModelError(nameof(DetailsModel.TrackingNumber), "Too long");

        var result = await model.OnPostMarkShippedAsync(42, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_OrderStatus", partial.ViewName);
        Assert.Equal(OrderStatus.New, model.Order.Status);
        Assert.Equal(0, service.UpdateCount);
    }

    [Fact]
    public async Task MarkShipped_PassesTheChosenCarrierWithTheTrackingNumber()
    {
        var service = new FakeAdminOrderService(Order()) { AfterUpdate = Order(status: OrderStatus.Shipped) };
        var model = CreateModel(service);
        model.TrackingNumber = "ABC123";
        model.Carrier = ShippingCarrier.Other;

        await model.OnPostMarkShippedAsync(42, CancellationToken.None);

        Assert.Equal(("ABC123", ShippingCarrier.Other), service.ShippedWith);
    }

    [Fact]
    public async Task SaveNote_WhenInvalid_SavesNothing()
    {
        var service = new FakeAdminOrderService(Order());
        var model = CreateModel(service);
        model.InternalNote = "Too long";
        model.ModelState.AddModelError(nameof(DetailsModel.InternalNote), "Too long");

        var result = await model.OnPostSaveNoteAsync(42, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_OrderNoteCard", partial.ViewName);
        Assert.Equal(0, service.NoteUpdateCount);
        Assert.Null(model.NoteStatusMessage);
    }

    [Fact]
    public async Task SaveNote_TooLongAfterNormalizing_ShowsAValidationMessage_AndSavesNothing()
    {
        var service = new FakeAdminOrderService(Order());
        var model = CreateModel(service);
        model.InternalNote = new string('x', OrderNotePolicy.MaxLength + 1);

        var result = await model.OnPostSaveNoteAsync(42, CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal(0, service.NoteUpdateCount);
    }

    [Fact]
    public async Task SaveNote_LengthIsCheckedOnTheNormalizedNote()
    {
        // Padding and CRLF line endings push the raw post over the limit; the stored note fits exactly.
        var lines = new string('x', OrderNotePolicy.MaxLength / 2 - 1);
        var service = new FakeAdminOrderService(Order());
        var model = CreateModel(service);
        model.InternalNote = $"   {lines}\r\n{lines}\r\n   ";

        await model.OnPostSaveNoteAsync(42, CancellationToken.None);

        Assert.True(model.ModelState.IsValid);
        Assert.Equal(1, service.NoteUpdateCount);
        Assert.Equal($"{lines}\n{lines}", model.Order.InternalNote);
    }

    [Fact]
    public async Task PackingSlip_ShowsShopOrderShipToAndItems_ButNoAdminOnlyDetails()
    {
        var order = Order(lines:
        [
            new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null),
            new OrderConfirmationLine("Linen Apron", 30.00m, 1, true, new DateOnly(2026, 11, 15)),
        ]) with { InternalNote = "Customer is a friend - add a freebie", TrackingNumber = "1Z999" };

        var html = await RenderSlipAsync(order);

        Assert.Contains("Glimpses of Glory", html);
        Assert.Contains("#42", html);
        Assert.Contains("August 1, 2026", html);
        Assert.Contains("Ship to", html);
        Assert.Contains("123 Main St", html);
        Assert.Contains("Springfield, IL 62704", html);
        Assert.Matches(@"Ceramic Mug\s*</td>\s*<td[^>]*>2</td>", html);
        Assert.Contains("Linen Apron", html);
        Assert.Contains("Preorder — ships approx. Nov 15, 2026", html);
        Assert.DoesNotContain("Customer is a friend", html);
        Assert.DoesNotContain("12.50", html);
        Assert.DoesNotContain("30.00", html);
        Assert.DoesNotContain("Total", html);
    }

    [Fact]
    public async Task ResendReadyForPickupEmail_Failed_ShowsAnErrorInsteadOfFailingSilently()
    {
        var service = new FakeAdminOrderService(Order(status: OrderStatus.ReadyForPickup, fulfillmentMethod: FulfillmentMethod.Pickup)) { ResendResult = EmailResendResult.Failed };
        var model = CreateModel(service);

        var result = await model.OnPostResendReadyForPickupEmailAsync(42, CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.Equal(["ready for pickup"], service.Resent);
        Assert.Null(model.StatusMessage);
        Assert.Equal("The ready-for-pickup email couldn't be sent. Please try again later.", model.ErrorMessage);
    }

    private static DetailsModel CreateModel(IAdminOrderService service) =>
        new(service)
        {
            // PageModel.Partial reads ViewData and metadata the framework normally supplies.
            MetadataProvider = new EmptyModelMetadataProvider(),
            PageContext = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new CompiledPageActionDescriptor()))
            {
                ViewData = new ViewDataDictionary<DetailsModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            },
        };

    // Just the slip itself (without the layout the renderer wraps it in), with entities decoded.
    private static async Task<string> RenderSlipAsync(AdminOrderDetail order)
    {
        var html = await RazorViewToStringRendererTests.CreateRenderer().RenderAsync("~/Pages/Admin/Orders/_PackingSlip.cshtml", order);
        return WebUtility.HtmlDecode(Regex.Match(html, "<article id=\"packing-slip\".*?</article>", RegexOptions.Singleline).Value);
    }

    private sealed class FakeAdminOrderService(AdminOrderDetail order) : IAdminOrderService
    {
        public AdminOrderDetail? AfterUpdate { get; init; }

        private AdminOrderDetail current = order;

        public int UpdateCount { get; private set; }

        public Task<AdminOrderPage> GetOrdersAsync(AdminOrderTab tab, AdminOrderFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminOrderPage([], 1, pageSize, 0));

        public Task<AdminOrderTabCounts> GetOrderTabCountsAsync(AdminOrderFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminOrderTabCounts(0, 0, 0, 0));

        public IAsyncEnumerable<AdminOrderExportRow> StreamOrdersForExportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<AdminOrderDetail?>(id == current.Id ? current : null);

        public Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, ShippingCarrier carrier, CancellationToken cancellationToken)
        {
            ShippedWith = (trackingNumber, carrier);
            return Update();
        }

        public (string? TrackingNumber, ShippingCarrier Carrier)? ShippedWith { get; private set; }

        public Task<OrderUpdateResult> MarkReadyForPickupAsync(int id, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken) => Update();

        // What the resend calls report; they never change the order.
        public EmailResendResult ResendResult { get; init; } = EmailResendResult.Sent;

        public List<string> Resent { get; } = [];

        public Task<EmailResendResult> ResendConfirmationAsync(int id, CancellationToken cancellationToken) => Resend(id, "confirmation");

        public Task<EmailResendResult> ResendShippedEmailAsync(int id, CancellationToken cancellationToken) => Resend(id, "shipped");

        public Task<EmailResendResult> ResendReadyForPickupEmailAsync(int id, CancellationToken cancellationToken) => Resend(id, "ready for pickup");

        private Task<EmailResendResult> Resend(int id, string kind)
        {
            if (id != current.Id)
            {
                return Task.FromResult(EmailResendResult.NotFound);
            }

            Resent.Add(kind);
            return Task.FromResult(ResendResult);
        }

        public int NoteUpdateCount { get; private set; }

        public Task<OrderUpdateResult> UpdateInternalNoteAsync(int id, string? note, CancellationToken cancellationToken)
        {
            if (id != current.Id)
            {
                return Task.FromResult(OrderUpdateResult.NotFound);
            }

            NoteUpdateCount++;
            current = current with { InternalNote = OrderNotePolicy.Normalize(note) };
            return Task.FromResult(OrderUpdateResult.Updated);
        }

        private Task<OrderUpdateResult> Update()
        {
            UpdateCount++;
            current = AfterUpdate ?? current;
            return Task.FromResult(OrderUpdateResult.Updated);
        }
    }
}
