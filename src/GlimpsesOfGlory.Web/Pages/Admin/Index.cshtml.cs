using System.Globalization;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin;

public class IndexModel(IAdminDashboardService dashboardService) : PageModel
{
    // Both order filters are subsets of the default (Needs action) tab, so its count matches the tile.
    private static readonly string OrdersToFulfillHref =
        new AdminOrderListState(AdminOrderTabs.Default, Search: null, PreorderOnly: false, Page: 1, ToFulfillOnly: true).Href;

    private static readonly string UnpaidCashOrdersHref =
        new AdminOrderListState(AdminOrderTabs.Default, Search: null, PreorderOnly: false, Page: 1, UnpaidCashOnly: true).Href;

    private static readonly string AbandonedCheckoutsHref =
        AdminSections.Links.Single(l => l.Section == AdminSection.AbandonedCheckouts).Href;

    public IReadOnlyList<DashboardTile> Tiles { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Tiles = BuildTiles(await dashboardService.GetMetricsAsync(cancellationToken));
    }

    public static IReadOnlyList<DashboardTile> BuildTiles(AdminDashboardMetrics metrics) =>
    [
        new("Orders to fulfill", Count(metrics.OrdersToFulfill), "New orders not yet shipped or ready for pickup.", OrdersToFulfillHref),
        new("Unpaid cash orders", Count(metrics.UnpaidCashOrders), "Cash still to collect on orders that aren't cancelled.", UnpaidCashOrdersHref),
        new("Low-stock products", Count(metrics.LowStockProducts), $"Visible products with {StockPolicy.LowStockThreshold} or fewer in stock, excluding preorders.", Products.IndexModel.LowStockHref),
        new("Abandoned checkouts not yet reminded", Count(metrics.UnremindedAbandonedCheckouts), "Checkouts from the last 30 days that never became an order and haven't had a reminder.", AbandonedCheckoutsHref),
        new("Revenue, last 7 days", Money(metrics.RevenueLast7Days), "Paid orders, excluding cancelled ones.", null),
        new("Revenue, last 30 days", Money(metrics.RevenueLast30Days), "Paid orders, excluding cancelled ones.", null),
    ];

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string Money(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);
}
