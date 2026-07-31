namespace GlimpsesOfGlory.Application.Cart;

public sealed class RemoveCartLineService(ICartStore cartStore)
{
    public async Task ExecuteAsync(string slug, CancellationToken cancellationToken)
    {
        var cart = await cartStore.GetCartAsync(cancellationToken);
        cart.RemoveLine(slug);
        await cartStore.SaveCartAsync(cart, cancellationToken);
    }
}
