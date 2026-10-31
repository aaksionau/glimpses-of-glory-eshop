using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Cart;

// The "Return to your cart" link from the abandoned-checkout reminder email.
public class RestoreModel(ICartRestoreService cartRestoreService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    // Shown as the cart page's banner.
    [TempData]
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken cancellationToken)
    {
        var result = await cartRestoreService.RestoreAsync(token, cancellationToken);
        if (result.Status == CartRestoreStatus.Expired)
        {
            Error = CheckoutMessages.RestoreLinkExpired;
            return RedirectToPage("/Cart/Index");
        }

        // A later checkout then updates the original Pending Checkout and PaymentIntent instead of minting new ones.
        checkoutSessionStore.SavePaymentIntentId(result.PaymentIntentId!);
        if (result.SkippedLineCount > 0)
        {
            Error = CheckoutMessages.RestoredItemsUnavailable;
        }

        return RedirectToPage("/Cart/Index");
    }
}
