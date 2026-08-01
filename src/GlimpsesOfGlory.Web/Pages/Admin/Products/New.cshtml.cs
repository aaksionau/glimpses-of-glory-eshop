using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public class NewModel(IAdminProductService adminProductService) : PageModel
{
    [BindProperty]
    public ProductFormInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new ProductEditRequest(Input.Name, Input.Description, Input.Price, Input.StockQuantity);
        var id = await adminProductService.CreateProductAsync(request, cancellationToken);

        return RedirectToPage("/Admin/Products/Edit", new { id });
    }
}
