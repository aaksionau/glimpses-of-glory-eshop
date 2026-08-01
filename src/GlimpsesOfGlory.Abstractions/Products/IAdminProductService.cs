namespace GlimpsesOfGlory.Abstractions.Products;

public interface IAdminProductService
{
    Task<IReadOnlyList<AdminProductSummary>> GetAllProductsAsync(CancellationToken cancellationToken);

    Task<AdminProductDetail?> GetProductAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateProductAsync(ProductEditRequest request, CancellationToken cancellationToken);

    Task<bool> UpdateProductAsync(int id, ProductEditRequest request, CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);

    Task<AdminProductPhoto?> AddPhotoAsync(int productId, Stream content, string originalFileName, CancellationToken cancellationToken);

    Task<bool> RemovePhotoAsync(int productId, int photoId, CancellationToken cancellationToken);

    Task<bool> MovePhotoAsync(int productId, int photoId, int direction, CancellationToken cancellationToken);
}
