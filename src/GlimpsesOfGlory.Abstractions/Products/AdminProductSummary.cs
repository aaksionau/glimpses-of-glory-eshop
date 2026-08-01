namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record AdminProductSummary(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    string? ThumbnailFileName);
