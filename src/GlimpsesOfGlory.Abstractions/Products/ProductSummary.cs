namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record ProductSummary(string Slug, string Name, decimal Price, string? ThumbnailFileName);
