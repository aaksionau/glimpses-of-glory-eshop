using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Cart;

public class IndexModel(ICartService cartService) : PageModel
{
    [TempData]
    public string? Error { get; set; }

    public CartSummary Cart { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await cartService.GetSummaryAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        var result = await cartService.UpdateLineQuantityAsync(slug, quantity, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);

        var error = result.Success ? null : new CartLineError(slug, result.ErrorMessage!);
        return Partial("_CartLines", new CartLinesView(Cart, error));
    }

    public async Task<IActionResult> OnPostRemoveAsync(string slug, CancellationToken cancellationToken)
    {
        await cartService.RemoveLineAsync(slug, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);
        return Partial("_CartLines", new CartLinesView(Cart, null));
    }
}
