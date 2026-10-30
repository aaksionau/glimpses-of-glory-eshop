using System.Globalization;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin;

public class IndexModel(IAdminDashboardService dashboardService) : PageModel
{
    public const string LowStockProductsHref = "/admin/products?lowStock=true";

    public static string OrdersToFulfillHref { get; } = OrdersHref(new AdminOrderFilter(ToFulfillOnly: true));

    public static string UnpaidCashOrdersHref { get; } = OrdersHref(new AdminOrderFilter(UnpaidCashOnly: true));

    public IReadOnlyList<DashboardTile> Tiles { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Tiles = BuildTiles(await dashboardService.GetMetricsAsync(cancellationToken));
    }

    public static IReadOnlyList<DashboardTile> BuildTiles(AdminDashboardMetrics metrics) =>
    [
        new("Orders to fulfill", Count(metrics.OrdersToFulfill), "New orders not yet shipped or ready for pickup.", OrdersToFulfillHref),
        new("Unpaid cash orders", Count(metrics.UnpaidCashOrders), "Cash still to collect on orders that aren't cancelled.", UnpaidCashOrdersHref),
        new("Low-stock products", Count(metrics.LowStockProducts), $"Visible products with {StockPolicy.LowStockThreshold} or fewer in stock, excluding preorders.", LowStockProductsHref),
        new("Revenue, last 7 days", metrics.RevenueLast7Days.ToString("C"), "Paid orders, excluding cancelled ones.", null),
        new("Revenue, last 30 days", metrics.RevenueLast30Days.ToString("C"), "Paid orders, excluding cancelled ones.", null),
    ];

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);

    // Both order filters are subsets of the default (Needs action) tab, so its count matches the tile.
    private static string OrdersHref(AdminOrderFilter filter) =>
        new AdminOrderListState(AdminOrderTabs.Default, null, false, 1, filter.ToFulfillOnly, filter.UnpaidCashOnly).Href;
}
