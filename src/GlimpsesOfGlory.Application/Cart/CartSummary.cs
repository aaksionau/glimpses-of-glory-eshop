namespace GlimpsesOfGlory.Application.Cart;

public sealed record CartLineView(string ProductSlug, string ProductName, string? ThumbnailFileName, decimal UnitPrice, int Quantity);

public sealed record CartSummary(IReadOnlyList<CartLineView> Lines, decimal Subtotal, decimal ShippingCost, decimal Total);
