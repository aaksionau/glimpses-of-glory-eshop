using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class ReviewModel(ICartService cartService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    public CartSummary Cart { get; private set; } = null!;

    public CheckoutAddress Address { get; private set; } = null!;

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

        Cart = cart;
        Address = address;
        return Page();
    }
}
