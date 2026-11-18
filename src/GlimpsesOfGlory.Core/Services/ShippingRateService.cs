using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.EntityConfigurations;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

// Reads rates per call (nothing cached) so admin edits take effect immediately.
public sealed class ShippingRateService(AppDbContext db) : IShippingRateService
{
    public async Task<ShippingRateView> GetDefaultRateAsync(CancellationToken cancellationToken)
    {
        var rate = await LoadDefaultRateAsync(cancellationToken);
        return new ShippingRateView(rate.FirstItem, rate.AdditionalItem);
    }

    public async Task UpdateDefaultRateAsync(ShippingRateView rate, CancellationToken cancellationToken)
    {
        await db.ShippingSettings
            .Where(s => s.Id == ShippingSettingConfiguration.DefaultId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.FirstItemAmount, rate.FirstItemAmount)
                    .SetProperty(s => s.AdditionalItemAmount, rate.AdditionalItemAmount),
                cancellationToken);
    }

    public async Task<decimal> QuoteAsync(IReadOnlyCollection<ShippingQuoteLine> lines, CancellationToken cancellationToken)
    {
        // An empty cart ships nothing; skip the lookups (this runs on every cart page view).
        if (!lines.Any(l => l.Quantity > 0))
        {
            return 0m;
        }

        var defaultRate = await LoadDefaultRateAsync(cancellationToken);
        var slugs = lines.Select(l => l.ProductSlug).ToList();
        var productRates = await db.Products
            .AsNoTracking()
            .Where(p => slugs.Contains(p.Slug))
            .Select(p => new { p.Slug, p.ShippingFirstItemAmount, p.ShippingAdditionalItemAmount })
            .ToDictionaryAsync(
                p => p.Slug,
                p => p.ShippingFirstItemAmount is { } first && p.ShippingAdditionalItemAmount is { } additional
                    ? new ShippingRate(first, additional)
                    : defaultRate,
                cancellationToken);

        return ShippingCalculator.Calculate(
            lines.Select(l => new ShippingItem(productRates.GetValueOrDefault(l.ProductSlug, defaultRate), l.Quantity)));
    }

    private async Task<ShippingRate> LoadDefaultRateAsync(CancellationToken cancellationToken) =>
        await db.ShippingSettings
            .AsNoTracking()
            .Where(s => s.Id == ShippingSettingConfiguration.DefaultId)
            .Select(s => new ShippingRate(s.FirstItemAmount, s.AdditionalItemAmount))
            .SingleAsync(cancellationToken);
}
