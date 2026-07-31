namespace GlimpsesOfGlory.Application.Products;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken);

    Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);
}
