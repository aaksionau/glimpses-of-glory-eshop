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
        Attempt = attempt;
        Order = await orderService.GetOrderConfirmationAsync(paymentIntent, cancellationToken);

        if (Order is not null)
        {
            await cartService.ClearAsync(cancellationToken);
            checkoutSessionStore.Clear();
        }

        return Page();
    }
}
