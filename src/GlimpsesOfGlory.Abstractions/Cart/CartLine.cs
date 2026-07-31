namespace GlimpsesOfGlory.Abstractions.Cart;

public sealed class CartLine
{
    public required string ProductSlug { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public string? ThumbnailFileName { get; set; }
    public int Quantity { get; set; }
}
