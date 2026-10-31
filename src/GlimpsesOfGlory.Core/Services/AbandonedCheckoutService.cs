using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

public sealed class AbandonedCheckoutService(AppDbContext db, TimeProvider timeProvider) : IAbandonedCheckoutService
{
    // Younger checkouts may still be mid-payment; older ones are too stale to chase.
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<AbandonedCheckout>> GetAbandonedCheckoutsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var newest = now - MinimumAge;
        var oldest = now - MaximumAge;
        // PendingCheckout.Total isn't mapped, so the sum is spelled out over its stored parts.
        return await db.PendingCheckouts
            .Where(p => p.CreatedAt < newest && p.CreatedAt > oldest)
            .Where(p => !db.Orders.Any(o => o.Email == p.Email && o.CreatedAt > p.CreatedAt))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AbandonedCheckout(
                p.Id,
                p.Email,
                p.Lines.OrderBy(l => l.Id).Select(l => new AbandonedCheckoutLine(l.ProductName, l.Quantity)).ToList(),
                p.Subtotal + p.ShippingCost,
                p.FulfillmentMethod,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
