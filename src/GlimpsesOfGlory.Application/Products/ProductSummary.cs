namespace GlimpsesOfGlory.Application.Products;

public sealed record ProductSummary(string Slug, string Name, decimal Price, string? ThumbnailFileName);
