using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface ICartStore
{
    Task<Cart> GetCartAsync(CancellationToken cancellationToken);

    Task SaveCartAsync(Cart cart, CancellationToken cancellationToken);
}
