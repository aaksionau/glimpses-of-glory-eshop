using System.Diagnostics.CodeAnalysis;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GlimpsesOfGlory.Web.Helpers;

// The redirects the checkout steps share: with an empty cart there's nothing to check out, so the
// shopper goes back to the cart; a step past the details form needs the saved address first.
public static class CheckoutStepGuard
{
    public static bool ShouldRedirect(CartSummary cart, [NotNullWhen(true)] out IActionResult? redirect)
    {
        redirect = cart.Lines.Count == 0 ? new RedirectToPageResult("/Cart/Index") : null;
        return redirect is not null;
    }

    public static bool ShouldRedirect([NotNullWhen(false)] CheckoutAddress? address, [NotNullWhen(true)] out IActionResult? redirect)
    {
        redirect = address is null ? new RedirectToPageResult("/Checkout/Index") : null;
        return redirect is not null;
    }

    public static bool ShouldRedirect(CartSummary cart, [NotNullWhen(false)] CheckoutAddress? address, [NotNullWhen(true)] out IActionResult? redirect) =>
        ShouldRedirect(cart, out redirect) || ShouldRedirect(address, out redirect);
}
