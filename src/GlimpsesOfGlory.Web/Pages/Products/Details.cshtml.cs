using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Products;

public class DetailsModel(IProductCatalogService productCatalogService, ICartService cartService) : PageModel
{
    public ProductDetail Product { get; private set; } = null!;

    public string? ErrorMessage { get; private set; }

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
        var result = await cartService.AddLineAsync(slug, quantity, cancellationToken);
        if (result.Success)
        {
            return RedirectToPage("/Cart/Index");
        }

        if (result.Product is null)
        {
            return NotFound();
        }

        Product = result.Product;
        ErrorMessage = result.ErrorMessage;
        return Page();
    }
}
