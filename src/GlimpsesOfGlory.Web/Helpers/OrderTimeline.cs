using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

// The steps shown on the admin order detail page: Placed -> Paid -> Shipped for shipping orders,
// Placed -> Paid -> Ready for pickup -> Picked up for pickup orders, or Placed -> (Paid) ->
// Cancelled once an order is cancelled.
public static class OrderTimeline
{
    public static IReadOnlyList<OrderTimelineStep> Steps(AdminOrderDetail order)
    {
        var steps = new List<OrderTimelineStep> { Step("Placed", true, order.CreatedAt) };
        var isCancelled = order.Status == OrderStatus.Cancelled;

        // A cancelled order can't be paid any more, so an unpaid one has no Paid step left to show.
        if (order.IsPaid || !isCancelled)
        {
            steps.Add(Step("Paid", order.IsPaid, order.PaidAt));
        }

        if (isCancelled)
        {
            steps.Add(Step(FulfillmentLabels.Status(OrderStatus.Cancelled), true, order.CancelledAt));
            return steps;
        }

        if (order.FulfillmentMethod == FulfillmentMethod.Pickup)
        {
            var isReady = order.Status is OrderStatus.ReadyForPickup or OrderStatus.PickedUp;
            steps.Add(Step(FulfillmentLabels.Status(OrderStatus.ReadyForPickup), isReady, order.ReadyForPickupAt));
            steps.Add(Step(FulfillmentLabels.Status(OrderStatus.PickedUp), order.Status == OrderStatus.PickedUp, order.PickedUpAt));
        }
        else
        {
            steps.Add(Step(FulfillmentLabels.Status(OrderStatus.Shipped), order.Status == OrderStatus.Shipped, order.ShippedAt));
        }

        return steps;
    }

    public static string Timestamp(OrderTimelineStep step) => step switch
    {
        { IsComplete: false } => "Pending",
        { At: { } at } => RelativeTime.FullTimestamp(at),
        _ => "Time not recorded",
    };

    // A pending step never carries a time, so the view can key off At alone.
    private static OrderTimelineStep Step(string label, bool isComplete, DateTimeOffset? at) =>
        new(label, isComplete, isComplete ? at : null);
}
