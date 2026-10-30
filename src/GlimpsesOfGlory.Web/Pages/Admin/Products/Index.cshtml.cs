using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public class IndexModel(IAdminProductService adminProductService) : PageModel
{
    // ?lowStock=true, linked from the dashboard's low-stock number. Also posted back by the
    // hide/unhide buttons so the re-rendered table keeps the filter.
    [BindProperty(SupportsGet = true)]
    public bool LowStock { get; set; }

    public AdminProductTable Table { get; private set; } = new([], false);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Table = await LoadTableAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is not null)
        {
            await adminProductService.SetActiveAsync(id, !product.IsActive, cancellationToken);
        }

        Table = await LoadTableAsync(cancellationToken);
        return Partial("_ProductTable", Table);
    }

    private async Task<AdminProductTable> LoadTableAsync(CancellationToken cancellationToken) =>
        new(await adminProductService.GetProductsAsync(LowStock, cancellationToken), LowStock);
}
