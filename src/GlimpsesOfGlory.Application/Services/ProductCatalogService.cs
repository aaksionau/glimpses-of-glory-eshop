namespace GlimpsesOfGlory.Application.Products;

public sealed class ProductCatalogService(IProductStore productStore)
{
    public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken) =>
        productStore.GetProductsAsync(cancellationToken);

    public Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken) =>
        productStore.GetProductBySlugAsync(slug, cancellationToken);
}
