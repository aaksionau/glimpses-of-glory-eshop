using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

public class DetailsModel(IAdminOrderService adminOrderService) : PageModel
{
    [BindProperty]
    [StringLength(200)]
    public string? TrackingNumber { get; set; }

    public AdminOrderDetail Order { get; private set; } = null!;

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        TrackingNumber = order.TrackingNumber;
        return Page();
    }

    // Swaps just the #order-status fragment (hx-target) so marking an order shipped/cancelled
    // never triggers a full page reload (AC requirement).
    public async Task<IActionResult> OnPostMarkShippedAsync(int id, CancellationToken cancellationToken)
    {
        var updated = await adminOrderService.MarkShippedAsync(id, TrackingNumber, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        StatusMessage = "Order marked as shipped and the buyer has been emailed.";
        return await OrderStatusPartialAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var updated = await adminOrderService.MarkCancelledAsync(id, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        StatusMessage = "Order marked as cancelled.";
        return await OrderStatusPartialAsync(id, cancellationToken);
    }

    private async Task<IActionResult> OrderStatusPartialAsync(int id, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        TrackingNumber = order.TrackingNumber;
        return Partial("_OrderStatus", this);
    }
}
