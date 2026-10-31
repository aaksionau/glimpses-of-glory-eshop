using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken);

    Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);

    // Keyed by product id; ids with no active product are left out.
    Task<IReadOnlyDictionary<int, ProductSummary>> GetProductsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
}
