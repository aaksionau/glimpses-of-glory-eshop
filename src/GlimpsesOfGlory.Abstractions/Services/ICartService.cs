using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface ICartService
{
    Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken);

    // Shipping isn't part of the cart summary - it depends on the fulfillment method
    // chosen at checkout (pickup is free).
    Task<decimal> GetShippingCostAsync(FulfillmentMethod fulfillmentMethod, CancellationToken cancellationToken);

    Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task RemoveLineAsync(string slug, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
