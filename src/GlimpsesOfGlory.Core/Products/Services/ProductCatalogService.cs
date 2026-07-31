using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Products.Services;

public sealed class ProductCatalogService(AppDbContext db) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummary(
                p.Slug,
                p.Name,
                p.Price,
                p.Photos.OrderBy(photo => photo.DisplayOrder).Select(photo => photo.FileName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new ProductDetail(
                p.Slug,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.Photos.OrderBy(photo => photo.DisplayOrder).Select(photo => photo.FileName).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
