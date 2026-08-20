using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Products;
using GlimpsesOfGlory.Abstractions.Shipping;
using GlimpsesOfGlory.Core.Shipping.Services;
using GlimpsesOfGlory.Core.Shipping.ValueObjects;
using CartModel = GlimpsesOfGlory.Abstractions.Cart.Cart;

namespace GlimpsesOfGlory.Core.Cart.Services;

public sealed class CartService(ICartStore cartStore, IProductCatalogService productCatalogService, IShippingSettingsService shippingSettingsService) : ICartService
{
    public async Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        var lines = cart.Lines
            .Select(l => new CartLineView(l.ProductSlug, l.ProductName, l.ThumbnailFileName, l.UnitPrice, l.Quantity, l.IsPreorder, l.ExpectedAvailabilityDate))
            .ToList();

        // Built per call (not injected as a singleton) so admin edits to shipping
        // tiers (#11) take effect immediately without a redeploy.
        var tiers = await shippingSettingsService.GetTiersAsync(cancellationToken);
        var shippingCalculator = new ShippingCalculator(
            tiers.Select(t => new ShippingTier(t.MinQuantity, t.Amount)).ToList());
        var shippingCost = shippingCalculator.Calculate(cart.TotalQuantity);

        return new CartSummary(lines, cart.Subtotal, shippingCost, cart.Subtotal + shippingCost);
    }

    public async Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return CartOperationResult.Ok();
        }

        var product = await productCatalogService.GetProductBySlugAsync(slug, cancellationToken);
        if (product is null)
        {
            return CartOperationResult.Failed("This product is no longer available.");
        }

        var cart = await cartStore.GetCartAsync(cancellationToken);
        var currentQuantity = cart.Lines.FirstOrDefault(l => l.ProductSlug == slug)?.Quantity ?? 0;
        var newQuantity = currentQuantity + quantity;

        var stockError = CheckStock(product, newQuantity);
        if (stockError is not null)
        {
            return stockError;
        }

        cart.SetLineQuantity(slug, product.Name, product.Price, product.PhotoFileNames.FirstOrDefault(), newQuantity, product.IsPreorder, product.ExpectedAvailabilityDate);
        await cartStore.SaveCartAsync(cart, cancellationToken);
        return CartOperationResult.Ok();
    }

    public async Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        if (quantity <= 0)
        {
            cart.RemoveLine(slug);
            await cartStore.SaveCartAsync(cart, cancellationToken);
            return CartOperationResult.Ok();
        }

        var product = await productCatalogService.GetProductBySlugAsync(slug, cancellationToken);
        if (product is null)
        {
            cart.RemoveLine(slug);
            await cartStore.SaveCartAsync(cart, cancellationToken);
            return CartOperationResult.Failed("This product is no longer available.");
        }

        var stockError = CheckStock(product, quantity);
        if (stockError is not null)
        {
            return stockError;
        }

        cart.SetLineQuantity(slug, product.Name, product.Price, product.PhotoFileNames.FirstOrDefault(), quantity, product.IsPreorder, product.ExpectedAvailabilityDate);
        await cartStore.SaveCartAsync(cart, cancellationToken);
        return CartOperationResult.Ok();
    }

    public async Task RemoveLineAsync(string slug, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);
        cart.RemoveLine(slug);
        await cartStore.SaveCartAsync(cart, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken) =>
        cartStore.SaveCartAsync(new CartModel(), cancellationToken);

    // Fixed abuse guard, not a configurable business setting - preorder stock has no
    // real ceiling, but an unbounded quantity per line would be exploitable.
    private const int MaxPreorderQuantityPerLine = 20;

    private static CartOperationResult? CheckStock(ProductDetail product, int requestedQuantity)
    {
        if (product.IsPreorder)
        {
            if (requestedQuantity > MaxPreorderQuantityPerLine)
            {
                return CartOperationResult.Failed($"Only up to {MaxPreorderQuantityPerLine} of {product.Name} can be preordered.", product);
            }

            return null;
        }

        if (product.StockQuantity <= 0)
        {
            return CartOperationResult.Failed($"{product.Name} is out of stock.", product);
        }

        if (requestedQuantity > product.StockQuantity)
        {
            return CartOperationResult.Failed($"Only {product.StockQuantity} of {product.Name} available.", product);
        }

        return null;
    }
}
