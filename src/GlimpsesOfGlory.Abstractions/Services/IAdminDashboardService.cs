using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminDashboardService
{
    // Computed in the database. The order counts use the same rules as the matching admin orders
    // list filters, and the low-stock count the same rule as the products list filter.
    Task<AdminDashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken);
}
