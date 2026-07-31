using GlimpsesOfGlory.Application.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Products;

public class DetailsModel(GetProductBySlug getProductBySlug) : PageModel
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
}
