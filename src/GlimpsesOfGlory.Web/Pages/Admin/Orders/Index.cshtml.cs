using GlimpsesOfGlory.Abstractions.Orders;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

public class IndexModel(IAdminOrderService adminOrderService) : PageModel
{
    public IReadOnlyList<AdminOrderSummary> Orders { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Orders = await adminOrderService.GetAllOrdersAsync(cancellationToken);
    }
}
