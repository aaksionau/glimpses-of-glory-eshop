using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Web.Checkout;
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
        int attempt,
        CancellationToken cancellationToken)
    {
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
