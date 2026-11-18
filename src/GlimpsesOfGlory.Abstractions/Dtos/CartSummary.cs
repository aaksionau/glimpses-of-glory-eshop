using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record CartLineView(
    string ProductSlug,
    string ProductName,
    string? ThumbnailFileName,
    decimal UnitPrice,
    int Quantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate);

// ShippingCost is what shipping the cart to an address costs
// (each product's shipping rate, or the store default); ShippingCostFor applies the chosen fulfillment
// method (local pickup is always free), and TotalFor adds it to the subtotal.
public sealed record CartSummary(IReadOnlyList<CartLineView> Lines, decimal Subtotal, decimal ShippingCost)
{
    public decimal ShippingCostFor(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? 0m : ShippingCost;

    public decimal TotalFor(FulfillmentMethod fulfillmentMethod) =>
        Subtotal + ShippingCostFor(fulfillmentMethod);
}
