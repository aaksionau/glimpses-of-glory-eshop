using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.UnitTests;

// ICartService double that always returns the given cart; clearing it is a no-op.
internal sealed class StubCartService(CartSummary cart) : ICartService
{
    public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(cart);

    public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task RemoveLineAsync(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
