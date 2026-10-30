using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class IndexModel(ICartService cartService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    [BindProperty]
    public CheckoutAddress Address { get; set; } = new();

    public CartSummary Cart { get; private set; } = null!;

    // Cost of the "Ship to my address" option; local pickup is free.
    public decimal ShippingCost => Cart.ShippingCost;

    // The delivery choice is still open here, so the summary follows the selected radio.
    public CheckoutOrderSummary Summary => new(Cart, Address.FulfillmentMethod, FollowsChoice: true);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

        Cart = cart;
        Address = checkoutSessionStore.GetAddress() ?? new CheckoutAddress();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

        Cart = cart;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        checkoutSessionStore.SaveAddress(Address);
        return RedirectToPage("/Checkout/Review");
    }
}
