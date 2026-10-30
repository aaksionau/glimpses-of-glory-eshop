using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

public class IndexModel(IAdminOrderService adminOrderService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public AdminOrderTabLink CurrentTab { get; private set; } = AdminOrderTabs.FromQuery(null);

    public AdminOrderTabCounts Counts { get; private set; } = new(0, 0, 0, 0);

    public IReadOnlyList<AdminOrderSummary> Orders { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentTab = AdminOrderTabs.FromQuery(Status);
        Counts = await adminOrderService.GetOrderTabCountsAsync(cancellationToken);
        Orders = await adminOrderService.GetOrdersAsync(CurrentTab.Tab, cancellationToken);
    }
}
