namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record ProductDetail(
    string Slug,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate,
    IReadOnlyList<string> PhotoFileNames);
