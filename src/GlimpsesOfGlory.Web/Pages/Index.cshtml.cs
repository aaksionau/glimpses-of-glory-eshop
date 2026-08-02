using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages;

public class IndexModel(IProductCatalogService productCatalogService) : PageModel
{
    private const int FeaturedProductCount = 6;

    public IReadOnlyList<ProductSummary> FeaturedProducts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var products = await productCatalogService.GetProductsAsync(cancellationToken);
        FeaturedProducts = [.. products.Take(FeaturedProductCount)];
    }
}
