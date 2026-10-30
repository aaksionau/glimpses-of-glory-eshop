using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Helpers;

public static class FulfillmentLabels
{
    // Label for the shipping-cost line in order totals.
    public static string CostLabel(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? "Pickup" : "Shipping";

    // What OrderStatus.Shipped means for this order - pickup orders are "shipped" once ready to collect.
    public static string ShippedStatus(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? "Ready for pickup" : "Shipped";

    // The customer email sent on OrderStatus.Shipped, as the admin order page names it.
    public static string ShippedEmailName(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? "ready-for-pickup email" : "shipping email";

    // What happens next, as the order confirmation page tells the buyer.
    public static string NextSteps(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup
            ? "We'll email you when your order is ready to collect."
            : "We'll email you a tracking number as soon as your order ships.";

    public static string Status(OrderStatus status, FulfillmentMethod fulfillmentMethod) => status switch
    {
        OrderStatus.Shipped => ShippedStatus(fulfillmentMethod),
        OrderStatus.PickedUp => "Picked up",
        _ => status.ToString(),
    };
}
