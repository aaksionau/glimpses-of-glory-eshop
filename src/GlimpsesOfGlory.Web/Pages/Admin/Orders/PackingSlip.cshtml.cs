using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

// Print-friendly packing slip for one order: what goes in the box and where it goes.
public class PackingSlipModel(IAdminOrderService adminOrderService) : PageModel
{
    public AdminOrderDetail Order { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        return Page();
    }
}
