using GlimpsesOfGlory.Domain;

namespace GlimpsesOfGlory.Application.Cart;

public sealed class GetCartSummary(ICartStore cartStore, ShippingCalculator shippingCalculator)
{
    public async Task<CartSummary> ExecuteAsync(CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);

        var lines = cart.Lines
            .Select(l => new CartLineView(l.ProductSlug, l.ProductName, l.ThumbnailFileName, l.UnitPrice, l.Quantity))
            .ToList();
        var shippingCost = shippingCalculator.Calculate(cart.TotalQuantity);

        return new CartSummary(lines, cart.Subtotal, shippingCost, cart.Subtotal + shippingCost);
    }
}
