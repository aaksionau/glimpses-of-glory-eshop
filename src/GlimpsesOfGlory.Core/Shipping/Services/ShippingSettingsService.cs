using GlimpsesOfGlory.Abstractions.Shipping;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Shipping.Services;

public sealed class ShippingSettingsService(AppDbContext db) : IShippingSettingsService
{
    public async Task<IReadOnlyList<ShippingTierView>> GetTiersAsync(CancellationToken cancellationToken)
    {
        return await db.ShippingTiers
            .AsNoTracking()
            .OrderBy(t => t.MinQuantity)
            .Select(t => new ShippingTierView(t.Id, t.MinQuantity, t.Amount))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateTiersAsync(IReadOnlyList<ShippingTierUpdate> tiers, CancellationToken cancellationToken)
    {
        foreach (var tier in tiers)
        {
            await db.ShippingTiers
                .Where(t => t.Id == tier.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.MinQuantity, tier.MinQuantity)
                        .SetProperty(t => t.Amount, tier.Amount),
                    cancellationToken);
        }
    }
}
