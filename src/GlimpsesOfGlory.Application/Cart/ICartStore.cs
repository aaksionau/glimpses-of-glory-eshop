using GlimpsesOfGlory.Domain;

namespace GlimpsesOfGlory.Application.Cart;

public interface ICartStore
{
    Task<GlimpsesOfGlory.Domain.Cart> GetCartAsync(CancellationToken cancellationToken);

    Task SaveCartAsync(GlimpsesOfGlory.Domain.Cart cart, CancellationToken cancellationToken);
}
