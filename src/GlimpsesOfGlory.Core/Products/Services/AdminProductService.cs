using GlimpsesOfGlory.Abstractions.Products;
using GlimpsesOfGlory.Core.Products.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Products.Services;

public sealed class AdminProductService(AppDbContext db, ProductPhotoStorage photoStorage) : IAdminProductService
{
    public async Task<IReadOnlyList<AdminProductSummary>> GetAllProductsAsync(CancellationToken cancellationToken)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AdminProductSummary(
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.StockQuantity,
                p.IsActive,
                p.Photos.OrderBy(photo => photo.DisplayOrder).Select(photo => photo.FileName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminProductDetail?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new AdminProductDetail(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.IsActive,
                p.Photos
                    .OrderBy(photo => photo.DisplayOrder)
                    .Select(photo => new AdminProductPhoto(photo.Id, photo.FileName, photo.DisplayOrder))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateProductAsync(ProductEditRequest request, CancellationToken cancellationToken)
    {
        var existingSlugs = await db.Products.Select(p => p.Slug).ToListAsync(cancellationToken);
        var slug = ProductSlugGenerator.Generate(request.Name, existingSlugs.ToHashSet());

        var product = new Product
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    public async Task<bool> UpdateProductAsync(int id, ProductEditRequest request, CancellationToken cancellationToken)
    {
        var rows = await db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Name, request.Name)
                    .SetProperty(p => p.Description, request.Description)
                    .SetProperty(p => p.Price, request.Price)
                    .SetProperty(p => p.StockQuantity, request.StockQuantity),
                cancellationToken);

        return rows > 0;
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var rows = await db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, isActive), cancellationToken);

        return rows > 0;
    }

    public async Task<AdminProductPhoto?> AddPhotoAsync(int productId, Stream content, string originalFileName, CancellationToken cancellationToken)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
        {
            return null;
        }

        var fileName = await photoStorage.SaveAsync(content, originalFileName, cancellationToken);

        var nextDisplayOrder = await db.ProductPhotos
            .Where(photo => photo.ProductId == productId)
            .Select(photo => (int?)photo.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var photoEntity = new ProductPhoto
        {
            ProductId = productId,
            FileName = fileName,
            DisplayOrder = nextDisplayOrder + 1,
        };

        db.ProductPhotos.Add(photoEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new AdminProductPhoto(photoEntity.Id, photoEntity.FileName, photoEntity.DisplayOrder);
    }

    public async Task<bool> RemovePhotoAsync(int productId, int photoId, CancellationToken cancellationToken)
    {
        var photo = await db.ProductPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.ProductId == productId, cancellationToken);
        if (photo is null)
        {
            return false;
        }

        db.ProductPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);

        photoStorage.Delete(photo.FileName);
        return true;
    }

    public async Task<bool> MovePhotoAsync(int productId, int photoId, int direction, CancellationToken cancellationToken)
    {
        if (direction != -1 && direction != 1)
        {
            return false;
        }

        var photos = await db.ProductPhotos
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var index = photos.FindIndex(p => p.Id == photoId);
        var swapIndex = index + direction;
        if (index < 0 || swapIndex < 0 || swapIndex >= photos.Count)
        {
            return false;
        }

        (photos[index].DisplayOrder, photos[swapIndex].DisplayOrder) = (photos[swapIndex].DisplayOrder, photos[index].DisplayOrder);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
