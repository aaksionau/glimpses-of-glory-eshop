namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record ProductSummary(
    string Slug,
    string Name,
    decimal Price,
    string? ThumbnailFileName,
    int StockQuantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate);
