namespace GlimpsesOfGlory.Abstractions.Cart;

public interface ICartStore
{
    Task<Cart> GetCartAsync(CancellationToken cancellationToken);

    Task SaveCartAsync(Cart cart, CancellationToken cancellationToken);
}
