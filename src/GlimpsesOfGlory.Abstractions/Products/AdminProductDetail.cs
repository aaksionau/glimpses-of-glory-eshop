namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record AdminProductDetail(
    int Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate,
    int PreorderedQuantity,
    IReadOnlyList<AdminProductPhoto> Photos);

public sealed record AdminProductPhoto(int Id, string FileName, int DisplayOrder);
