namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record AdminProductSummary(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    bool IsPreorder,
    string? ThumbnailFileName);
