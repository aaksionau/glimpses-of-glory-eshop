using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Formatting;

public static class FulfillmentLabels
{
    // Label for the shipping-cost line in order totals.
    public static string CostLabel(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? "Pickup" : "Shipping";

    // What happens next, as the order confirmation page tells the buyer.
    public static string NextSteps(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup
            ? "We'll email you when your order is ready to collect."
            : "We'll email you a tracking number as soon as your order ships.";

    public static string Status(OrderStatus status) => status switch
    {
        OrderStatus.ReadyForPickup => "Ready for pickup",
        OrderStatus.PickedUp => "Picked up",
        _ => status.ToString(),
    };
}
