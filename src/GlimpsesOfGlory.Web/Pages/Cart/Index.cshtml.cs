using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Cart;

public class IndexModel(ICartService cartService) : PageModel
{
    [TempData]
    public string? Error { get; set; }

    // The banner above the cart: checkout's generic message when it had no line-level detail,
    // or when none of the lines it flagged are still in the cart.
    public string? Banner { get; private set; }

    public CartSummary Cart { get; private set; } = null!;

    // Per-line messages from a checkout that ran short of stock.
    public IReadOnlyList<CartLineError> LineErrors { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await cartService.GetSummaryAsync(cancellationToken);

        var reported = CartLineErrorsTempData.Read(TempData);
        LineErrors = reported.Where(e => Cart.Lines.Any(l => l.ProductSlug == e.ProductSlug)).ToList();
        Banner = Error ?? (reported.Count > 0 && LineErrors.Count == 0 ? CheckoutMessages.ItemsUnavailable : null);
    }

    public async Task<IActionResult> OnPostUpdateQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var result = await cartService.UpdateLineQuantityAsync(slug, quantity, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);

        IReadOnlyList<CartLineError> errors = result.Success ? [] : [new CartLineError(slug, result.ErrorMessage!)];
        return Partial("_CartLines", new CartLinesView(Cart, errors));
    }

    // The removed line comes back with an Undo, so a mis-tap doesn't silently lose it.
    public async Task<IActionResult> OnPostRemoveAsync(string slug, CancellationToken cancellationToken)
    {
        var removed = (await cartService.GetSummaryAsync(cancellationToken)).Lines.FirstOrDefault(l => l.ProductSlug == slug);
        await cartService.RemoveLineAsync(slug, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);
        return Partial("_CartLines", new CartLinesView(Cart, [], RemovedLine: removed));
    }

    // Puts a removed line back. It's re-added like any add to cart, so stock and price are checked again.
    public async Task<IActionResult> OnPostUndoRemoveAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var result = await cartService.AddLineAsync(slug, quantity, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);
        return Partial("_CartLines", new CartLinesView(Cart, [], Message: result.Success ? null : result.ErrorMessage));
    }
}
