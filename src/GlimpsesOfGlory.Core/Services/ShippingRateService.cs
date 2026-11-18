using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

// Reads rates per call (nothing cached) so admin edits take effect immediately.
public sealed class ShippingRateService(AppDbContext db) : IShippingRateService
{
    public Task<ShippingRate> GetDefaultRateAsync(CancellationToken cancellationToken) =>
        db.ShippingSettings
            .AsNoTracking()
            .Where(s => s.Id == ShippingSetting.DefaultId)
            .Select(s => new ShippingRate(s.FirstItemAmount, s.AdditionalItemAmount))
            .SingleAsync(cancellationToken);

    public async Task UpdateDefaultRateAsync(ShippingRate rate, CancellationToken cancellationToken)
    {
        await db.ShippingSettings
            .Where(s => s.Id == ShippingSetting.DefaultId)
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

        var slugs = lines.Select(l => l.ProductSlug).ToList();
        var ownRates = await db.Products
            .AsNoTracking()
            .Where(p => slugs.Contains(p.Slug) && p.ShippingRate != null)
            .Select(p => new { p.Slug, Rate = p.ShippingRate! })
            .ToDictionaryAsync(p => p.Slug, p => p.Rate, cancellationToken);

        // The default is only read when some line needs it.
        var defaultRate = lines.All(l => ownRates.ContainsKey(l.ProductSlug))
            ? null
            : await GetDefaultRateAsync(cancellationToken);

        return ShippingCalculator.Calculate(
            lines.Select(l => new ShippingItem(ownRates.GetValueOrDefault(l.ProductSlug) ?? defaultRate!, l.Quantity)));
    }
}
