namespace GlimpsesOfGlory.Domain;

public sealed class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public List<ProductPhoto> Photos { get; set; } = [];
}
