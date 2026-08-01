using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class IndexModel(ICartService cartService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    [BindProperty]
    public CheckoutAddress Address { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

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

        if (!ModelState.IsValid)
        {
            return Page();
        }

        checkoutSessionStore.SaveAddress(Address);
        return RedirectToPage("/Checkout/Review");
    }
}
