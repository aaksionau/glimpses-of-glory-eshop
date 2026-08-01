using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Products;
using GlimpsesOfGlory.Abstractions.Shipping;
using GlimpsesOfGlory.Core.Shipping.Services;
using GlimpsesOfGlory.Core.Shipping.ValueObjects;

namespace GlimpsesOfGlory.Core.Cart.Services;

public sealed class CartService(ICartStore cartStore, IProductCatalogService productCatalogService, IShippingSettingsService shippingSettingsService) : ICartService
{
    public async Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        var lines = cart.Lines
            .Select(l => new CartLineView(l.ProductSlug, l.ProductName, l.ThumbnailFileName, l.UnitPrice, l.Quantity))
            .ToList();

        // Built per call (not injected as a singleton) so admin edits to shipping
        // tiers (#11) take effect immediately without a redeploy.
        var tiers = await shippingSettingsService.GetTiersAsync(cancellationToken);
        var shippingCalculator = new ShippingCalculator(
            tiers.Select(t => new ShippingTier(t.MinQuantity, t.Amount)).ToList());
        var shippingCost = shippingCalculator.Calculate(cart.TotalQuantity);

        return new CartSummary(lines, cart.Subtotal, shippingCost, cart.Subtotal + shippingCost);
    }

    public async Task AddLineAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return;
        }

        var product = await productCatalogService.GetProductBySlugAsync(slug, cancellationToken);
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

    public async Task UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        if (quantity <= 0)
        {
            cart.RemoveLine(slug);
        }
        else
        {
            var product = await productCatalogService.GetProductBySlugAsync(slug, cancellationToken);
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

    public async Task RemoveLineAsync(string slug, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);
        cart.RemoveLine(slug);
        await cartStore.SaveCartAsync(cart, cancellationToken);
    }
}
