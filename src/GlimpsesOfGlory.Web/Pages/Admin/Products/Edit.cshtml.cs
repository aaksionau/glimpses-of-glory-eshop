using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Products;

public sealed record PhotoListViewModel(int ProductId, IReadOnlyList<AdminProductPhoto> Photos);

public class EditModel(IAdminProductService adminProductService) : PageModel
{
    [BindProperty]
    public ProductFormInput Input { get; set; } = new();

    public int Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Slug { get; private set; } = "";

    public bool IsActive { get; private set; }

    // The saved values, for the header and sidebar: Input holds the posted (possibly invalid) ones after a failed save.
    public bool IsPreorder { get; private set; }

    public int StockQuantity { get; private set; }

    public DateOnly? ExpectedAvailabilityDate { get; private set; }

    public IReadOnlyList<AdminProductPhoto> Photos { get; private set; } = [];

    public int PreorderedQuantity { get; private set; }

    public string? SavedMessage { get; private set; }

    public string? VisibilityMessage { get; private set; }

    public string? StockMessage { get; private set; }

    // True when rendering the htmx response to an action: the fragments outside the swap target
    // (header badges, sidebar) are then sent as out-of-band swaps.
    public bool IsHtmxUpdate { get; private set; }

    // The hx-swap-oob value for those fragments; null drops the attribute on a full page render.
    public string? OutOfBandSwap => IsHtmxUpdate ? "true" : null;

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

    // Swaps just the #product-form fragment (hx-target) so saving edits never triggers a
    // full page reload (AC requirement); the sidebar and header badges follow out-of-band.
    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidProduct = await adminProductService.GetProductAsync(id, cancellationToken);
            if (invalidProduct is null)
            {
                return NotFound();
            }

            LoadSavedState(invalidProduct);
            IsHtmxUpdate = true;
            return Partial("_ProductForm", this);
        }

        var request = new ProductEditRequest(Input.Name, Input.Description, Input.Price, Input.StockQuantity, Input.IsPreorder, Input.ExpectedAvailabilityDate);
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
        SavedMessage = "Changes saved.";
        IsHtmxUpdate = true;
        return Partial("_ProductForm", this);
    }

    // Posted with hx-swap="none": only the sidebar and header badges change (out-of-band),
    // so unsaved edits in the details form survive a visibility toggle.
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

        LoadSavedState(refreshed);
        VisibilityMessage = refreshed.IsActive ? "Now visible on the storefront." : "Now hidden from the storefront.";
        IsHtmxUpdate = true;
        return Partial("_ProductSidebar", this);
    }

    // Receiving stock rewrites the stock and preorder fields, so the details form is re-rendered too.
    public async Task<IActionResult> OnPostReceiveStockAsync(int id, int quantityReceived, CancellationToken cancellationToken)
    {
        await adminProductService.ReceiveStockAsync(id, quantityReceived, cancellationToken);

        var product = await adminProductService.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        LoadFrom(product);
        StockMessage = "Stock received.";
        IsHtmxUpdate = true;
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
        LoadSavedState(product);
        Input = new ProductFormInput
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsPreorder = product.IsPreorder,
            ExpectedAvailabilityDate = product.ExpectedAvailabilityDate,
        };
    }

    private void LoadSavedState(AdminProductDetail product)
    {
        Id = product.Id;
        Name = product.Name;
        Slug = product.Slug;
        IsActive = product.IsActive;
        IsPreorder = product.IsPreorder;
        StockQuantity = product.StockQuantity;
        ExpectedAvailabilityDate = product.ExpectedAvailabilityDate;
        Photos = product.Photos;
        PreorderedQuantity = product.PreorderedQuantity;
    }

    private async Task<IActionResult> PhotoListPartialAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await adminProductService.GetProductAsync(productId, cancellationToken);
        var photos = product?.Photos ?? [];
        return Partial("_PhotoList", new PhotoListViewModel(productId, photos));
    }
}
