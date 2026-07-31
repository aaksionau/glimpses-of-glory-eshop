namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record ProductDetail(
    string Slug,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    IReadOnlyList<string> PhotoFileNames);
