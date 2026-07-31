using GlimpsesOfGlory.Application.Products;

namespace GlimpsesOfGlory.Application.Cart;

public sealed class AddCartLineService(ICartStore cartStore, IProductStore productStore)
{
    public async Task ExecuteAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var product = await productStore.GetProductBySlugAsync(slug, cancellationToken);
        if (product is null)
        {
            return;
        }

        var cart = await cartStore.GetCartAsync(cancellationToken);
        var currentQuantity = cart.Lines.FirstOrDefault(l => l.ProductSlug == slug)?.Quantity ?? 0;
        var newQuantity = Math.Min(currentQuantity + quantity, product.StockQuantity);

        cart.SetLineQuantity(slug, product.Name, product.Price, product.PhotoFileNames.FirstOrDefault(), newQuantity);
        await cartStore.SaveCartAsync(cart, cancellationToken);
    }
}
