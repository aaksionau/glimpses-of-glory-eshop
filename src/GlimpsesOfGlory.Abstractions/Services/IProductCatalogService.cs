using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken);

    Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken);
}
