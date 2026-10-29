using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class OrderTimelineTests
{
    private static readonly DateTimeOffset Placed = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Paid = new(2026, 8, 1, 9, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Shipped = new(2026, 8, 2, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PickedUp = new(2026, 8, 3, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cancelled = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    private static AdminOrderDetail Order(
        OrderStatus status,
        FulfillmentMethod fulfillmentMethod = FulfillmentMethod.Shipping,
        DateTimeOffset? paidAt = null,
        DateTimeOffset? shippedAt = null,
        DateTimeOffset? pickedUpAt = null,
        DateTimeOffset? cancelledAt = null,
        bool? isPaid = null) =>
        new(
            Id: 42,
            FulfillmentMethod: fulfillmentMethod,
            PaymentMethod: PaymentMethod.Cash,
            IsPaid: isPaid ?? paidAt is not null,
            PaymentDashboardUrl: null,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [],
            Subtotal: 0m,
            ShippingCost: 0m,
            Total: 0m,
            Status: status,
            TrackingNumber: null,
            CreatedAt: Placed,
            PaidAt: paidAt,
            ShippedAt: shippedAt,
            PickedUpAt: pickedUpAt,
            CancelledAt: cancelledAt,
            CanMarkShipped: false,
            CanMarkPickedUp: false,
            CanMarkPaid: false,
            CanMarkCancelled: false,
            NeedsRefund: false);

    [Fact]
    public void NewUnpaidShippingOrder_HasPlacedCompleteAndTheRestPending()
    {
        var steps = OrderTimeline.Steps(Order(OrderStatus.New));

        Assert.Equal(
            [new OrderTimelineStep("Placed", true, Placed), new("Paid", false, null), new("Shipped", false, null)],
            steps);
    }

    [Fact]
    public void ShippedShippingOrder_HasNoPickedUpStep()
    {
        var steps = OrderTimeline.Steps(Order(OrderStatus.Shipped, paidAt: Paid, shippedAt: Shipped));

        Assert.Equal(
            [new OrderTimelineStep("Placed", true, Placed), new("Paid", true, Paid), new("Shipped", true, Shipped)],
            steps);
    }

    [Fact]
    public void PickupOrder_UsesReadyForPickup_ThenPickedUp()
    {
        var ready = OrderTimeline.Steps(Order(OrderStatus.Shipped, FulfillmentMethod.Pickup, shippedAt: Shipped));
        var collected = OrderTimeline.Steps(Order(OrderStatus.PickedUp, FulfillmentMethod.Pickup, Paid, Shipped, PickedUp));

        Assert.Equal(
            [new OrderTimelineStep("Placed", true, Placed), new("Paid", false, null), new("Ready for pickup", true, Shipped), new("Picked up", false, null)],
            ready);
        Assert.All(collected, step => Assert.True(step.IsComplete));
        Assert.Equal(PickedUp, collected[^1].At);
    }

    [Fact]
    public void CancelledOrder_EndsAtCancelled_AndKeepsPaidOnlyIfPaid()
    {
        var unpaid = OrderTimeline.Steps(Order(OrderStatus.Cancelled, cancelledAt: Cancelled));
        var paid = OrderTimeline.Steps(Order(OrderStatus.Cancelled, paidAt: Paid, cancelledAt: Cancelled));

        Assert.Equal([new OrderTimelineStep("Placed", true, Placed), new("Cancelled", true, Cancelled)], unpaid);
        Assert.Equal(
            [new OrderTimelineStep("Placed", true, Placed), new("Paid", true, Paid), new("Cancelled", true, Cancelled)],
            paid);
    }

    [Fact]
    public void Timestamp_ShowsDateAndTime_PendingOrNotRecorded()
    {
        Assert.Equal("August 2, 2026 at 14:30 UTC", OrderTimeline.Timestamp(new("Shipped", true, Shipped)));
        Assert.Equal("Pending", OrderTimeline.Timestamp(new("Shipped", false, null)));
        // Orders that reached a step before step timestamps were recorded.
        Assert.Equal("Time not recorded", OrderTimeline.Timestamp(new("Shipped", true, null)));
    }
}
