using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Products;

public class IndexModel(IProductCatalogService productCatalogService) : PageModel
{
    public IReadOnlyList<ProductSummary> Products { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Products = await productCatalogService.GetProductsAsync(cancellationToken);
    }
}
