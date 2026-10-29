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

    public static string Status(OrderStatus status, FulfillmentMethod fulfillmentMethod) =>
        status == OrderStatus.Shipped ? ShippedStatus(fulfillmentMethod) : status.ToString();
}
