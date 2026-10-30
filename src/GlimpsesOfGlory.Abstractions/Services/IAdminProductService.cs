using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminProductService
{
    // Ordered by name. lowStockOnly keeps visible, non-preorder products at or below
    // StockPolicy.LowStockThreshold - the same rule as the admin dashboard's low-stock number.
    Task<IReadOnlyList<AdminProductSummary>> GetProductsAsync(bool lowStockOnly, CancellationToken cancellationToken);

    Task<AdminProductDetail?> GetProductAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateProductAsync(ProductEditRequest request, CancellationToken cancellationToken);

    Task<bool> UpdateProductAsync(int id, ProductEditRequest request, CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);

    Task<bool> ReceiveStockAsync(int id, int quantityReceived, CancellationToken cancellationToken);

    Task<AdminProductPhoto?> AddPhotoAsync(int productId, Stream content, string originalFileName, CancellationToken cancellationToken);

    Task<bool> RemovePhotoAsync(int productId, int photoId, CancellationToken cancellationToken);

    Task<bool> MovePhotoAsync(int productId, int photoId, int direction, CancellationToken cancellationToken);
}
