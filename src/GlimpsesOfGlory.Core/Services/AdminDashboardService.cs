using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

public sealed class AdminDashboardService(AppDbContext db, TimeProvider timeProvider) : IAdminDashboardService
{
    // One query per metric; the DbContext can't run them concurrently.
    public async Task<AdminDashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var toFulfill = await db.Orders.CountAsync(AdminQueryFilters.ToFulfill, cancellationToken);
        var unpaidCash = await db.Orders.CountAsync(AdminQueryFilters.UnpaidCash, cancellationToken);
        var lowStock = await db.Products.CountAsync(AdminQueryFilters.LowStock, cancellationToken);
        var revenue7 = await RevenueSinceAsync(now.AddDays(-7), cancellationToken);
        var revenue30 = await RevenueSinceAsync(now.AddDays(-30), cancellationToken);
        return new AdminDashboardMetrics(toFulfill, unpaidCash, lowStock, revenue7, revenue30);
    }

    // Order.Total isn't mapped, so the sum is spelled out over its stored parts.
    private async Task<decimal> RevenueSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        await db.Orders
            .Where(o => o.PaidAt != null && o.PaidAt >= since && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.Subtotal + o.ShippingCost, cancellationToken);
}
