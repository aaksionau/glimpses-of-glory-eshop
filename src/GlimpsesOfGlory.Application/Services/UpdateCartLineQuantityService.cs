using GlimpsesOfGlory.Application.Products;

namespace GlimpsesOfGlory.Application.Cart;

public sealed class UpdateCartLineQuantityService(ICartStore cartStore, IProductStore productStore)
{
    public async Task ExecuteAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        if (quantity <= 0)
        {
            cart.RemoveLine(slug);
        }
        else
        {
            var product = await productStore.GetProductBySlugAsync(slug, cancellationToken);
            if (product is null)
            {
                cart.RemoveLine(slug);
            }
            else
            {
                var clampedQuantity = Math.Min(quantity, product.StockQuantity);
                cart.SetLineQuantity(slug, product.Name, product.Price, product.PhotoFileNames.FirstOrDefault(), clampedQuantity);
            }
        }

        await cartStore.SaveCartAsync(cart, cancellationToken);
    }
}
