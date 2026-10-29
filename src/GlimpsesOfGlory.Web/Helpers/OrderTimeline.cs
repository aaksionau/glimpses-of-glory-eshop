using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

// The steps shown on the admin order detail page: Placed -> Paid -> Shipped / Ready for pickup ->
// (Picked up, pickup orders only), or Placed -> (Paid) -> Cancelled once an order is cancelled.
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
            steps.Add(Step(Label(OrderStatus.Cancelled), true, order.CancelledAt));
            return steps;
        }

        var isShipped = order.Status is OrderStatus.Shipped or OrderStatus.PickedUp;
        steps.Add(Step(Label(OrderStatus.Shipped), isShipped, order.ShippedAt));

        if (order.FulfillmentMethod == FulfillmentMethod.Pickup)
        {
            steps.Add(Step(Label(OrderStatus.PickedUp), order.Status == OrderStatus.PickedUp, order.PickedUpAt));
        }

        return steps;

        string Label(OrderStatus status) => FulfillmentLabels.Status(status, order.FulfillmentMethod);
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
