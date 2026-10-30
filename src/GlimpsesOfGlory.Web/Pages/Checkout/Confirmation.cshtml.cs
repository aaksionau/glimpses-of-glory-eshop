using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class ConfirmationModel(IOrderService orderService, ICartService cartService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    public const int MaxPollAttempts = 5;

    public string PaymentIntentId { get; private set; } = string.Empty;

    public int Attempt { get; private set; }

    public OrderConfirmationView? Order { get; private set; }

    // True when htmx is polling for the payment result: only the status fragment is rendered.
    public bool IsHtmxUpdate => Request.Headers["HX-Request"] == "true";

    public bool IsPolling => Order is null && Attempt < MaxPollAttempts;

    public string NextPollUrl =>
        $"/checkout/confirmation?payment_intent={Uri.EscapeDataString(PaymentIntentId)}&attempt={Attempt + 1}";

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "payment_intent")] string? paymentIntent,
        [FromQuery(Name = "order")] int? orderId,
        int attempt,
        CancellationToken cancellationToken)
    {
        // Cash orders: placed synchronously on the review page, so there's nothing to poll for.
        if (orderId is not null)
        {
            if (checkoutSessionStore.GetPlacedOrderId() != orderId)
            {
                return RedirectToPage("/Cart/Index");
            }

            Order = await orderService.GetOrderConfirmationAsync(orderId.Value, cancellationToken);
            return Order is null ? RedirectToPage("/Cart/Index") : Page();
        }

        if (string.IsNullOrEmpty(paymentIntent))
        {
            return RedirectToPage("/Cart/Index");
        }

        PaymentIntentId = paymentIntent;
        // The attempt count comes from the client; clamp it so a forged value can't poll forever.
        Attempt = Math.Clamp(attempt, 0, MaxPollAttempts);
        Order = await orderService.GetOrderConfirmationAsync(paymentIntent, cancellationToken);

        if (Order is not null)
        {
            await cartService.ClearAsync(cancellationToken);
            checkoutSessionStore.Clear();
        }

        // The same URL serves the full page and the htmx fragment, so caches must key on the htmx header.
        Response.Headers.Vary = "HX-Request";
        return IsHtmxUpdate ? Partial("_PaymentPoll", this) : Page();
    }
}
