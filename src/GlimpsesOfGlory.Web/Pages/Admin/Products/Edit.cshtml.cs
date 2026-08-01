using GlimpsesOfGlory.Abstractions.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public sealed record PhotoListViewModel(int ProductId, IReadOnlyList<AdminProductPhoto> Photos);

public class EditModel(IAdminProductService adminProductService) : PageModel
{
    [BindProperty]
    public ProductFormInput Input { get; set; } = new();

    public int Id { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<AdminProductPhoto> Photos { get; private set; } = [];

    public string? SavedMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        LoadFrom(product);
        return Page();
    }

    // Swaps just the #product-form fragment (hx-target) so saving edits or
    // toggling visibility never triggers a full page reload (AC requirement).
    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidProduct = await adminProductService.GetProductAsync(id, cancellationToken);
            if (invalidProduct is null)
            {
                return NotFound();
            }

            Id = invalidProduct.Id;
            IsActive = invalidProduct.IsActive;
            Photos = invalidProduct.Photos;
            return Partial("_ProductForm", this);
        }

        var request = new ProductEditRequest(Input.Name, Input.Description, Input.Price, Input.StockQuantity);
        var updated = await adminProductService.UpdateProductAsync(id, request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        LoadFrom(product);
        SavedMessage = "Product updated.";
        return Partial("_ProductForm", this);
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        await adminProductService.SetActiveAsync(id, !product.IsActive, cancellationToken);

        var refreshed = await adminProductService.GetProductAsync(id, cancellationToken);
        if (refreshed is null)
        {
            return NotFound();
        }

        LoadFrom(refreshed);
        return Partial("_ProductForm", this);
    }

    public async Task<IActionResult> OnPostUploadPhotoAsync(int id, IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is { Length: > 0 })
        {
            await using var stream = photo.OpenReadStream();
            await adminProductService.AddPhotoAsync(id, stream, photo.FileName, cancellationToken);
        }

        return await PhotoListPartialAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostRemovePhotoAsync(int id, int photoId, CancellationToken cancellationToken)
    {
        await adminProductService.RemovePhotoAsync(id, photoId, cancellationToken);
        return await PhotoListPartialAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostMovePhotoAsync(int id, int photoId, int direction, CancellationToken cancellationToken)
    {
        await adminProductService.MovePhotoAsync(id, photoId, direction, cancellationToken);
        return await PhotoListPartialAsync(id, cancellationToken);
    }

    private void LoadFrom(AdminProductDetail product)
    {
        Id = product.Id;
        IsActive = product.IsActive;
        Photos = product.Photos;
        Input = new ProductFormInput
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
        };
    }

    private async Task<IActionResult> PhotoListPartialAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(productId, cancellationToken);
        var photos = product?.Photos ?? [];
        return Partial("_PhotoList", new PhotoListViewModel(productId, photos));
    }
}
