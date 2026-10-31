using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.TestSupport;

// In-memory ICartStore; Cart is whatever was last saved.
internal sealed class FakeCartStore : ICartStore
{
    public Cart Cart { get; private set; } = new();

    public Task<Cart> GetCartAsync(CancellationToken cancellationToken) => Task.FromResult(Cart);

    public Task SaveCartAsync(Cart cart, CancellationToken cancellationToken)
    {
        Cart = cart;
        return Task.CompletedTask;
    }
}
