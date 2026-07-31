using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Products;

public class DetailsModel(IProductCatalogService productCatalogService, ICartService cartService) : PageModel
{
    public ProductDetail Product { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await productCatalogService.GetProductBySlugAsync(slug, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        await cartService.AddLineAsync(slug, quantity, cancellationToken);
        return RedirectToPage("/Cart/Index");
    }
}
