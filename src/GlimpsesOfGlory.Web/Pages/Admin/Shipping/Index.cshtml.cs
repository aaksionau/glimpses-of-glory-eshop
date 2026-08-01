using GlimpsesOfGlory.Abstractions.Shipping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Shipping;

public class IndexModel(IShippingSettingsService shippingSettingsService) : PageModel
{
    [BindProperty]
    public List<ShippingTierInput> Tiers { get; set; } = [];

    public string? SavedMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadTiersAsync(cancellationToken);
    }

    // Swaps just the #shipping-form fragment (hx-target) so saving never triggers
    // a full page reload (AC requirement).
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_ShippingForm", this);
        }

        var updates = Tiers
            .Select(t => new ShippingTierUpdate(t.Id, t.MinQuantity, t.Amount))
            .ToList();
        await shippingSettingsService.UpdateTiersAsync(updates, cancellationToken);

        SavedMessage = "Shipping rates updated.";
        await LoadTiersAsync(cancellationToken);
        return Partial("_ShippingForm", this);
    }

    private async Task LoadTiersAsync(CancellationToken cancellationToken)
    {
        var tiers = await shippingSettingsService.GetTiersAsync(cancellationToken);
        Tiers = tiers
            .OrderBy(t => t.MinQuantity)
            .Select(t => new ShippingTierInput { Id = t.Id, MinQuantity = t.MinQuantity, Amount = t.Amount })
            .ToList();
    }
}
