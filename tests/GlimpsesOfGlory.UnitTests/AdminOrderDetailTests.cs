using System.Text.RegularExpressions;
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
            CreatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            PaidAt: isPaid ? new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero) : null,
            ShippedAt: null,
            PickedUpAt: null,
            CancelledAt: null,
            CanMarkShipped: status == OrderStatus.New,
            CanMarkPickedUp: status == OrderStatus.Shipped && fulfillmentMethod == FulfillmentMethod.Pickup,
            CanMarkPaid: !isPaid && status != OrderStatus.Cancelled,
            CanMarkCancelled: status == OrderStatus.New,
            NeedsRefund: status == OrderStatus.Cancelled && paymentMethod == PaymentMethod.Card && isPaid);

    [Fact]
    public void Primary_NewOrder_IsMarkShipped()
    {
        Assert.Equal(OrderAction.MarkShipped, OrderActions.Primary(Order()));
        Assert.Empty(OrderActions.Secondary(Order()));
    }

    [Fact]
    public void Primary_NewUnpaidCashOrder_IsFulfilment_WithMarkPaidSecondary()
    {
        var order = Order(fulfillmentMethod: FulfillmentMethod.Pickup, paymentMethod: PaymentMethod.Cash, isPaid: false);

        Assert.Equal(OrderAction.MarkShipped, OrderActions.Primary(order));
        Assert.Equal([OrderAction.MarkPaid], OrderActions.Secondary(order));
    }

    [Fact]
    public void Primary_ReadyForPickupOrder_IsMarkPickedUp()
    {
        var order = Order(status: OrderStatus.Shipped, fulfillmentMethod: FulfillmentMethod.Pickup);

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
    public void EveryOrderAction_HasAMatchingPageHandler()
    {
        // _OrderStatus posts to ?handler={action}, so each enum member must name an OnPost{Action}Async handler.
        foreach (var action in Enum.GetValues<OrderAction>())
        {
            Assert.NotNull(typeof(DetailsModel).GetMethod($"OnPost{action}Async"));
        }
    }

    [Theory]
    [InlineData(OrderAction.MarkShipped, FulfillmentMethod.Shipping, "Mark shipped")]
    [InlineData(OrderAction.MarkShipped, FulfillmentMethod.Pickup, "Mark ready for pickup")]
    [InlineData(OrderAction.MarkPickedUp, FulfillmentMethod.Pickup, "Mark picked up")]
    [InlineData(OrderAction.MarkPaid, FulfillmentMethod.Pickup, "Mark as paid")]
    public void Label_NamesTheAction(OrderAction action, FulfillmentMethod fulfillmentMethod, string expected)
    {
        Assert.Equal(expected, OrderActions.Label(action, fulfillmentMethod));
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
    public void ExpectedAvailability_FormatsTheDate()
    {
        Assert.Equal("Expected available Nov 3, 2026", PreorderNoteFormatter.ExpectedAvailability(new DateOnly(2026, 11, 3)));
        Assert.Equal("No expected availability date", PreorderNoteFormatter.ExpectedAvailability(null));
    }

    [Fact]
    public async Task ActionsFragment_ShowsOnlyTheNextStepAsPrimary_AndCancelSeparately()
    {
        var order = Order(fulfillmentMethod: FulfillmentMethod.Pickup, paymentMethod: PaymentMethod.Cash, isPaid: false);
        var model = await RunMarkPaidHandlerAsync(order, order);

        var html = await RenderAsync("~/Pages/Admin/Orders/_OrderStatus.cshtml", model);

        Assert.Single(Regex.Matches(html, "data-primary=\"true\""));
        Assert.Matches("data-order-action=\"MarkShipped\" data-primary=\"true\"", html);
        Assert.Matches("data-order-action=\"MarkPaid\" data-primary=\"false\"", html);
        Assert.Contains("data-order-action=\"MarkCancelled\"", html);
    }

    [Fact]
    public async Task ActionsFragment_AfterHtmxAction_SwapsHeaderBadgesAndCardsOutOfBand()
    {
        var before = Order(status: OrderStatus.PickedUp, fulfillmentMethod: FulfillmentMethod.Pickup, paymentMethod: PaymentMethod.Cash, isPaid: false);
        var after = before with { IsPaid = true, CanMarkPaid = false };
        var model = await RunMarkPaidHandlerAsync(before, after);

        var html = await RenderAsync("~/Pages/Admin/Orders/_OrderStatus.cshtml", model);

        Assert.Matches("id=\"order-header-badges\"[^>]*hx-swap-oob=\"true\"", html);
        Assert.Matches("id=\"order-payment\"[^>]*hx-swap-oob=\"true\"", html);
        Assert.Matches("id=\"order-fulfillment\"[^>]*hx-swap-oob=\"true\"", html);
        Assert.Matches("id=\"order-timeline\"[^>]*hx-swap-oob=\"true\"", html);
        var headerBadges = Regex.Match(html, "id=\"order-header-badges\".*?</div>", RegexOptions.Singleline).Value;
        Assert.Contains(">Paid<", headerBadges);
        Assert.Contains(">Picked up<", headerBadges);
        Assert.DoesNotContain("data-primary=\"true\"", html);
    }

    [Fact]
    public async Task HeaderBadges_OnFullPageRender_AreNotOutOfBand()
    {
        var model = new DetailsModel(new FakeAdminOrderService(Order()));
        await model.OnGetAsync(42, CancellationToken.None);

        var html = await RenderAsync("~/Pages/Admin/Orders/_OrderHeaderBadges.cshtml", model);

        Assert.Contains("id=\"order-header-badges\"", html);
        Assert.DoesNotContain("hx-swap-oob", html);
    }

    [Fact]
    public async Task Timeline_ShowsCompletedStepsWithTime_AndPendingStepsGreyedOut()
    {
        var order = Order(status: OrderStatus.Shipped, fulfillmentMethod: FulfillmentMethod.Pickup) with
        {
            ShippedAt = new DateTimeOffset(2026, 8, 2, 14, 30, 0, TimeSpan.Zero),
        };
        var model = new DetailsModel(new FakeAdminOrderService(order));
        await model.OnGetAsync(42, CancellationToken.None);

        var html = await RenderAsync("~/Pages/Admin/Orders/_OrderTimeline.cshtml", model);

        Assert.DoesNotContain("hx-swap-oob", html);
        Assert.Matches("data-timeline-step=\"Ready for pickup\" data-complete=\"true\"", html);
        Assert.Contains("August 2, 2026 at 14:30 UTC", html);
        Assert.Matches("text-gray-400\" data-timeline-step=\"Picked up\" data-complete=\"false\"", html);
        Assert.Contains("Pending", html);
    }

    // Runs the real MarkPaid handler, so the model is in the state its htmx response renders.
    private static async Task<DetailsModel> RunMarkPaidHandlerAsync(AdminOrderDetail before, AdminOrderDetail after)
    {
        var service = new FakeAdminOrderService(before) { AfterUpdate = after };
        var model = CreateModel(service);

        var result = await model.OnPostMarkPaidAsync(before.Id, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_OrderStatus", partial.ViewName);
        return Assert.IsType<DetailsModel>(partial.Model);
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

    private static Task<string> RenderAsync(string viewPath, DetailsModel model) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(viewPath, model);

    private sealed class FakeAdminOrderService(AdminOrderDetail order) : IAdminOrderService
    {
        public AdminOrderDetail? AfterUpdate { get; init; }

        private AdminOrderDetail current = order;

        public int UpdateCount { get; private set; }

        public Task<IReadOnlyList<AdminOrderSummary>> GetOrdersAsync(AdminOrderTab tab, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminOrderSummary>>([]);

        public Task<AdminOrderTabCounts> GetOrderTabCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AdminOrderTabCounts(0, 0, 0, 0));

        public Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<AdminOrderDetail?>(id == current.Id ? current : null);

        public Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) => Update();

        public Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken) => Update();

        private Task<OrderUpdateResult> Update()
        {
            UpdateCount++;
            current = AfterUpdate ?? current;
            return Task.FromResult(OrderUpdateResult.Updated);
        }
    }
}
