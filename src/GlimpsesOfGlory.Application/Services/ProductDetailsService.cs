namespace GlimpsesOfGlory.Application.Products;

public sealed class ProductDetailsService(IProductStore productStore)
{
    public Task<ProductDetail?> ExecuteAsync(string slug, CancellationToken cancellationToken) =>
        productStore.GetProductBySlugAsync(slug, cancellationToken);
}
