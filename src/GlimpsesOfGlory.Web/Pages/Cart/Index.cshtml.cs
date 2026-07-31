using GlimpsesOfGlory.Application.Cart;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Cart;

public class IndexModel(CartService cartService) : PageModel
{
    public CartSummary Cart { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await cartService.GetSummaryAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        await cartService.UpdateLineQuantityAsync(slug, quantity, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);
        return Partial("_CartLines", Cart);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string slug, CancellationToken cancellationToken)
    {
        await cartService.RemoveLineAsync(slug, cancellationToken);
        Cart = await cartService.GetSummaryAsync(cancellationToken);
        return Partial("_CartLines", Cart);
    }
}
