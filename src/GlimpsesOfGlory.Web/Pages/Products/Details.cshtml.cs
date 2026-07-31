using GlimpsesOfGlory.Application.Cart;
using GlimpsesOfGlory.Application.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Products;

public class DetailsModel(GetProductBySlug getProductBySlug, AddCartLine addCartLine) : PageModel
{
    public ProductDetail Product { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await getProductBySlug.ExecuteAsync(slug, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, int quantity, CancellationToken cancellationToken)
    {
        await addCartLine.ExecuteAsync(slug, quantity, cancellationToken);
        return RedirectToPage("/Cart/Index");
    }
}
