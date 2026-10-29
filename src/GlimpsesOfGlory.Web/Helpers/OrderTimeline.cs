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
        var steps = new List<OrderTimelineStep> { new("Placed", true, order.CreatedAt) };

        if (order.Status == OrderStatus.Cancelled)
        {
            // A cancelled order can't be paid any more, so an unpaid one has no Paid step left to show.
            if (order.IsPaid)
            {
                steps.Add(new("Paid", true, order.PaidAt));
            }
            steps.Add(new("Cancelled", true, order.CancelledAt));
            return steps;
        }

        steps.Add(new("Paid", order.IsPaid, order.PaidAt));

        var isShipped = order.Status is OrderStatus.Shipped or OrderStatus.PickedUp;
        steps.Add(new(FulfillmentLabels.ShippedStatus(order.FulfillmentMethod), isShipped, order.ShippedAt));

        if (order.FulfillmentMethod == FulfillmentMethod.Pickup)
        {
            steps.Add(new("Picked up", order.Status == OrderStatus.PickedUp, order.PickedUpAt));
        }

        return steps;
    }

    public static string Timestamp(OrderTimelineStep step) => step switch
    {
        { IsComplete: false } => "Pending",
        { At: { } at } => RelativeTime.FullTimestamp(at),
        _ => "Time not recorded",
    };
}
