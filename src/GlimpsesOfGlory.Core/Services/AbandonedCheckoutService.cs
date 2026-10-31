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
        var createdBefore = now - MinimumAge;
        var createdAfter = now - MaximumAge;
        return await db.PendingCheckouts
            .Where(p => p.CreatedAt < createdBefore && p.CreatedAt > createdAfter)
            .Where(p => !db.Orders.Any(o => o.Email == p.Email && o.CreatedAt > p.CreatedAt))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AbandonedCheckout(
                p.Id,
                p.Email,
                p.Lines.OrderBy(l => l.Id).Select(l => new AbandonedCheckoutLine(l.ProductName, l.Quantity)).ToList(),
                p.Total,
                p.FulfillmentMethod,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
