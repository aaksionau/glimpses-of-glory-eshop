using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class PaymentModel(
    ICartService cartService,
    CheckoutSessionStore checkoutSessionStore,
    IOrderService orderService,
    IOptions<StripeOptions> stripeOptions) : PageModel
{
    public string PublishableKey { get; private set; } = string.Empty;

    public string ClientSecret { get; private set; } = string.Empty;

    // Built from the cart as priced for the PaymentIntent, so its total is what the card is charged.
    public CheckoutOrderSummary Summary { get; private set; } = null!;

    public decimal AmountDue => Summary.Total;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

        var address = checkoutSessionStore.GetAddress();
        if (address is null)
        {
            return RedirectToPage("/Checkout/Index");
        }

        var existingPaymentIntentId = checkoutSessionStore.GetPaymentIntentId();
        var setup = await orderService.CreatePaymentIntentAsync(address.FulfillmentMethod, address.ToShippingAddressInfo(), existingPaymentIntentId, cancellationToken);

        if (setup is null)
        {
            TempData["Error"] = CheckoutMessages.ItemsUnavailable;
            return RedirectToPage("/Cart/Index");
        }

        checkoutSessionStore.SavePaymentIntentId(setup.PaymentIntentId);
        PublishableKey = stripeOptions.Value.PublishableKey;
        ClientSecret = setup.ClientSecret;
        Summary = new CheckoutOrderSummary(setup.Cart, address.FulfillmentMethod);
        return Page();
    }
}
