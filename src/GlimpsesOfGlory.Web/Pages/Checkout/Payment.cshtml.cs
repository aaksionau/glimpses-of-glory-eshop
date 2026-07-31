using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Web.Checkout;
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

        var setup = await orderService.CreatePaymentIntentAsync(
            new ShippingAddressInfo(
                address.Email,
                address.FullName,
                address.AddressLine1,
                address.AddressLine2,
                address.City,
                address.State,
                address.PostalCode,
                address.Country),
            cancellationToken);

        if (setup is null)
        {
            TempData["Error"] = "Sorry, one or more items in your cart are no longer available in the requested quantity.";
            return RedirectToPage("/Cart/Index");
        }

        PublishableKey = stripeOptions.Value.PublishableKey;
        ClientSecret = setup.ClientSecret;
        return Page();
    }
}
