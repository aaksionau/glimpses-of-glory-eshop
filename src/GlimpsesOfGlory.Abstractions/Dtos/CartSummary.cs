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

// ShippingCost is what shipping to an address costs for the cart's current quantity
// (from the admin shipping tiers); ShippingCostFor applies the chosen fulfillment
// method (local pickup is always free).
public sealed record CartSummary(IReadOnlyList<CartLineView> Lines, decimal Subtotal, decimal ShippingCost)
{
    public decimal ShippingCostFor(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? 0m : ShippingCost;
}
