using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
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

        return await OrderStatusPartialAsync(
            id,
            order => $"Order marked as {FulfillmentLabels.ShippedStatus(order.FulfillmentMethod).ToLowerInvariant()} and the buyer has been emailed.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id, CancellationToken cancellationToken)
    {
        var updated = await adminOrderService.MarkPaidAsync(id, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return await OrderStatusPartialAsync(id, _ => "Order marked as paid.", cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var updated = await adminOrderService.MarkCancelledAsync(id, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return await OrderStatusPartialAsync(id, _ => "Order marked as cancelled.", cancellationToken);
    }

    // statusMessage gets the reloaded order so the wording can depend on it (e.g. shipped vs. ready for pickup).
    private async Task<IActionResult> OrderStatusPartialAsync(int id, Func<AdminOrderDetail, string> statusMessage, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        TrackingNumber = order.TrackingNumber;
        StatusMessage = statusMessage(order);
        return Partial("_OrderStatus", this);
    }
}
