using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Constants;
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

    // Only recorded when a tracking number is entered. EnumDataType rejects posted values outside the enum.
    [BindProperty]
    [EnumDataType(typeof(ShippingCarrier))]
    public ShippingCarrier Carrier { get; set; } = ShippingCarrier.Usps;

    // Length is checked in OnPostSaveNoteAsync on the normalized note (what gets stored), not the raw post.
    [BindProperty]
    [Display(Name = "Internal note")]
    public string? InternalNote { get; set; }

    public AdminOrderDetail Order { get; private set; } = null!;

    // Confirmation shown inside the note card after an htmx save.
    public string? NoteStatusMessage { get; private set; }

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    // True when rendering the htmx response to an action: the fragments outside #order-status
    // (header badges, fulfillment and payment cards) are then sent as out-of-band swaps.
    public bool IsHtmxUpdate { get; private set; }

    // The hx-swap-oob value for those fragments; null drops the attribute on a full page render.
    public string? OutOfBandSwap => IsHtmxUpdate ? "true" : null;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        ShowOrder(order);
        return Page();
    }

    // Swaps only the #order-note card (hx-target), so saving a note never reloads the page.
    public async Task<IActionResult> OnPostSaveNoteAsync(int id, CancellationToken cancellationToken)
    {
        if (OrderNotePolicy.Normalize(InternalNote)?.Length > OrderNotePolicy.MaxLength)
        {
            ModelState.AddModelError(nameof(InternalNote), $"The note can be at most {OrderNotePolicy.MaxLength} characters.");
        }

        // When invalid, nothing is saved and the card re-renders with the validation message and the admin's text.
        var isValid = ModelState.IsValid;
        if (isValid && await adminOrderService.UpdateInternalNoteAsync(id, InternalNote, cancellationToken) == OrderUpdateResult.NotFound)
        {
            return NotFound();
        }

        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        if (isValid)
        {
            // Show the stored (normalized) note, not the raw input.
            ModelState.Remove(nameof(InternalNote));
            InternalNote = order.InternalNote;
            NoteStatusMessage = order.InternalNote is null ? "Note cleared." : "Note saved.";
        }

        return Partial("_OrderNoteCard", this);
    }

    // Swaps the #order-status actions fragment (hx-target) so marking an order shipped/cancelled
    // never triggers a full page reload; the header badges and the fulfillment/payment cards ride
    // along as out-of-band swaps so they stay in sync.
    public async Task<IActionResult> OnPostMarkShippedAsync(int id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            // Re-render with the validation message and the admin's input; nothing is saved.
            var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
            if (order is null)
            {
                return NotFound();
            }

            Order = order;
            IsHtmxUpdate = true;
            return Partial("_OrderStatus", this);
        }

        var result = await adminOrderService.MarkShippedAsync(id, TrackingNumber, Carrier, cancellationToken);

        return await OrderStatusPartialAsync(
            id,
            result,
            "Order marked as shipped and the buyer has been emailed.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkReadyForPickupAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkReadyForPickupAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, "Order marked as ready for pickup and the buyer has been emailed.", cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkPickedUpAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkPickedUpAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, "Order marked as picked up.", cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkPaidAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, "Order marked as paid.", cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var result = await adminOrderService.MarkCancelledAsync(id, cancellationToken);

        return await OrderStatusPartialAsync(id, result, "Order marked as cancelled.", cancellationToken);
    }

    // Resends swap the same #order-status fragment, showing whether the email went out. They change
    // nothing on the order, so there's nothing to swap out of band.
    public Task<IActionResult> OnPostResendConfirmationAsync(int id, CancellationToken cancellationToken) =>
        ResendEmailPartialAsync(id, adminOrderService.ResendConfirmationAsync, "order confirmation email", cancellationToken);

    public Task<IActionResult> OnPostResendShippedEmailAsync(int id, CancellationToken cancellationToken) =>
        ResendEmailPartialAsync(id, adminOrderService.ResendShippedEmailAsync, "shipping email", cancellationToken);

    public Task<IActionResult> OnPostResendReadyForPickupEmailAsync(int id, CancellationToken cancellationToken) =>
        ResendEmailPartialAsync(id, adminOrderService.ResendReadyForPickupEmailAsync, "ready-for-pickup email", cancellationToken);

    private async Task<IActionResult> ResendEmailPartialAsync(int id, Func<int, CancellationToken, Task<EmailResendResult>> resend, string emailName, CancellationToken cancellationToken)
    {
        var result = await resend(id, cancellationToken);
        if (result == EmailResendResult.NotFound)
        {
            return NotFound();
        }

        return await ReloadPartialAsync(id, order =>
        {
            switch (result)
            {
                case EmailResendResult.Sent:
                    StatusMessage = $"The {emailName} was resent to {order.Address.Email}.";
                    break;
                case EmailResendResult.NotAllowed:
                    ErrorMessage = $"The {emailName} can't be resent for this order's current status. Nothing was sent.";
                    break;
                default:
                    ErrorMessage = $"The {emailName} couldn't be sent. Please try again later.";
                    break;
            }
        }, cancellationToken);
    }

    private async Task<IActionResult> OrderStatusPartialAsync(int id, OrderUpdateResult result, string statusMessage, CancellationToken cancellationToken)
    {
        if (result == OrderUpdateResult.NotFound)
        {
            return NotFound();
        }

        return await ReloadPartialAsync(id, order =>
        {
            IsHtmxUpdate = true;
            if (result == OrderUpdateResult.InvalidTransition)
            {
                // Usually a stale page (the order changed in another tab) - the re-rendered
                // fragment shows the current status and only the actions it still allows.
                ErrorMessage = "That action isn't allowed for this order's current status. Nothing was changed.";
            }
            else
            {
                StatusMessage = statusMessage;
            }
        }, cancellationToken);
    }

    // Reloads the order and re-renders the #order-status fragment; setMessages fills in the outcome.
    private async Task<IActionResult> ReloadPartialAsync(int id, Action<AdminOrderDetail> setMessages, CancellationToken cancellationToken)
    {
        var order = await adminOrderService.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        ShowOrder(order);
        setMessages(order);
        return Partial("_OrderStatus", this);
    }

    // Loads the order and seeds the editable fields from what is stored.
    private void ShowOrder(AdminOrderDetail order)
    {
        Order = order;
        TrackingNumber = order.TrackingNumber;
        Carrier = order.Carrier ?? ShippingCarrier.Usps;
        InternalNote = order.InternalNote;
    }
}
