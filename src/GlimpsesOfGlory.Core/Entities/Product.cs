namespace GlimpsesOfGlory.Core.Entities;

public sealed class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPreorder { get; set; }
    public DateOnly? ExpectedAvailabilityDate { get; set; }
    public int PreorderedQuantity { get; set; }
    public List<ProductPhoto> Photos { get; set; } = [];

    // Preorder lines have no stock ceiling to check against.
    public bool CanFulfill(int quantity) => IsPreorder || StockQuantity >= quantity;
}
