namespace GlimpsesOfGlory.Abstractions.Cart;

public interface ICartService
{
    Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken);

    Task AddLineAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task RemoveLineAsync(string slug, CancellationToken cancellationToken);
}
