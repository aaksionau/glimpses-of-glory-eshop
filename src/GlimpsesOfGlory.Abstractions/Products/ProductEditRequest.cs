namespace GlimpsesOfGlory.Abstractions.Products;

public sealed record ProductEditRequest(string Name, string Description, decimal Price, int StockQuantity);
