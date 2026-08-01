using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public class IndexModel(IAdminProductService adminProductService) : PageModel
{
    public IReadOnlyList<AdminProductSummary> Products { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Products = await adminProductService.GetAllProductsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is not null)
        {
            await adminProductService.SetActiveAsync(id, !product.IsActive, cancellationToken);
        }

        Products = await adminProductService.GetAllProductsAsync(cancellationToken);
        return Partial("_ProductTable", Products);
    }
}
