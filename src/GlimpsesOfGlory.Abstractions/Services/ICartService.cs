using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface ICartService
{
    Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken);

    // The fulfillment method is chosen at checkout: pickup is free, shipping costs the
    // same as CartSummary.ShippingCost.
    Task<decimal> GetShippingCostAsync(FulfillmentMethod fulfillmentMethod, CancellationToken cancellationToken);

    Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken);

    Task RemoveLineAsync(string slug, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
