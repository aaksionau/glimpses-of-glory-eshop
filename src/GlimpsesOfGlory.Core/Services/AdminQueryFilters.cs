using System.Linq.Expressions;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// SQL-translatable filters shared by the admin lists and the admin dashboard, so a dashboard
// number always matches the list it links to. Order filters come from OrderLifecycle's rules.
internal static class AdminQueryFilters
{
    // Younger checkouts may still be mid-payment; older ones are too stale to chase.
    private static readonly TimeSpan AbandonedCheckoutMinimumAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan AbandonedCheckoutMaximumAge = TimeSpan.FromDays(30);

    // Visible, stock-tracked products running out. Preorder products have no stock ceiling.
    public static readonly Expression<Func<Product, bool>> LowStock =
        p => p.IsActive && !p.IsPreorder && p.StockQuantity <= StockPolicy.LowStockThreshold;

    // Pending checkouts between one hour and thirty days old that the same email hasn't since
    // turned into an Order.
    public static IQueryable<PendingCheckout> AbandonedCheckouts(AppDbContext db, DateTimeOffset now)
    {
        var createdBefore = now - AbandonedCheckoutMinimumAge;
        var createdAfter = now - AbandonedCheckoutMaximumAge;
        return db.PendingCheckouts
            .Where(p => p.CreatedAt < createdBefore && p.CreatedAt > createdAfter)
            .Where(p => !db.Orders.Any(o => o.Email == p.Email && o.CreatedAt > p.CreatedAt));
    }
}
