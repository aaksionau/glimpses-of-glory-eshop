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
// (from the admin shipping tiers); local pickup is always free.
public sealed record CartSummary(IReadOnlyList<CartLineView> Lines, decimal Subtotal, decimal ShippingCost);
