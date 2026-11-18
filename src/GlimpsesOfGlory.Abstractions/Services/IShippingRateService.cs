namespace GlimpsesOfGlory.Abstractions.Services;

public interface IShippingRateService
{
    // The store default, used for products without their own shipping rate.
    Task<ShippingRateView> GetDefaultRateAsync(CancellationToken cancellationToken);

    Task UpdateDefaultRateAsync(ShippingRateView rate, CancellationToken cancellationToken);

    // What shipping these cart lines to an address costs, each product at its own rate
    // (or the store default). Lines for products that no longer exist ship at the default.
    Task<decimal> QuoteAsync(IReadOnlyCollection<ShippingQuoteLine> lines, CancellationToken cancellationToken);
}

public sealed record ShippingRateView(decimal FirstItemAmount, decimal AdditionalItemAmount);

public sealed record ShippingQuoteLine(string ProductSlug, int Quantity);
