using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface ICartService
{
    Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken);

    Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task RemoveLineAsync(string slug, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
