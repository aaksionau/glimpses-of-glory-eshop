namespace GlimpsesOfGlory.Abstractions.Products;

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken);

    Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);
}
