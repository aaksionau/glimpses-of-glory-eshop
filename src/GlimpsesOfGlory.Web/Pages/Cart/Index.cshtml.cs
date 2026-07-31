using GlimpsesOfGlory.Application.Cart;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Cart;

public class IndexModel(
    CartSummaryService cartSummaryService,
    UpdateCartLineQuantityService updateCartLineQuantityService,
    RemoveCartLineService removeCartLineService) : PageModel
{
    public CartSummary Cart { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await cartSummaryService.ExecuteAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateQuantityAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        await updateCartLineQuantityService.ExecuteAsync(slug, quantity, cancellationToken);
        Cart = await cartSummaryService.ExecuteAsync(cancellationToken);
        return Partial("_CartLines", Cart);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string slug, CancellationToken cancellationToken)
    {
        await removeCartLineService.ExecuteAsync(slug, cancellationToken);
        Cart = await cartSummaryService.ExecuteAsync(cancellationToken);
        return Partial("_CartLines", Cart);
    }
}
