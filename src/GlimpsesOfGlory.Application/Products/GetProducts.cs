namespace GlimpsesOfGlory.Application.Products;

public sealed class GetProducts(IProductStore productStore)
{
    public Task<IReadOnlyList<ProductSummary>> ExecuteAsync(CancellationToken cancellationToken) =>
        productStore.GetProductsAsync(cancellationToken);
}
