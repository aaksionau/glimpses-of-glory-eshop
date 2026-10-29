using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
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

    public string? ErrorMessage { get; private set; }

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
        var result = await adminOrderService.MarkShippedAsync(id, TrackingNumber, cancellationToken);

        return await OrderStatusPartialAsync(
            id,
            result,
            order => $"Order marked as {FulfillmentLabels.ShippedStatus(order.FulfillmentMethod).ToLowerInvariant()} and the buyer has been emailed.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkPaidAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, _ => "Order marked as paid.", cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkCancelledAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, _ => "Order marked as cancelled.", cancellationToken);
    }

    // statusMessage gets the reloaded order so the wording can depend on it (e.g. shipped vs. ready for pickup).
    private async Task<IActionResult> OrderStatusPartialAsync(int id, OrderUpdateResult result, Func<AdminOrderDetail, string> statusMessage, CancellationToken cancellationToken)
    {
        if (result == OrderUpdateResult.NotFound)
        {
            return NotFound();
        }

        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        TrackingNumber = order.TrackingNumber;
        if (result == OrderUpdateResult.InvalidTransition)
        {
            // Usually a stale page (the order changed in another tab) - the re-rendered
            // fragment shows the current status and only the actions it still allows.
            ErrorMessage = "That action isn't allowed for this order's current status. Nothing was changed.";
        }
        else
        {
            StatusMessage = statusMessage(order);
        }
        return Partial("_OrderStatus", this);
    }
}
