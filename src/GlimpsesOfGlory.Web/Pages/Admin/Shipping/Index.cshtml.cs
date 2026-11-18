using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Shipping;

public class IndexModel(IShippingRateService shippingRateService) : PageModel
{
    [BindProperty]
    public ShippingRateInput DefaultRate { get; set; } = new();

    public string? SavedMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDefaultRateAsync(cancellationToken);
    }

    // Swaps just the #shipping-form fragment (hx-target) so saving never triggers
    // a full page reload (AC requirement).
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_ShippingForm", this);
        }

        await shippingRateService.UpdateDefaultRateAsync(
            new ShippingRateView(DefaultRate.FirstItemAmount, DefaultRate.AdditionalItemAmount),
            cancellationToken);

        SavedMessage = "Default shipping rate updated.";
        await LoadDefaultRateAsync(cancellationToken);
        return Partial("_ShippingForm", this);
    }

    private async Task LoadDefaultRateAsync(CancellationToken cancellationToken)
    {
        var rate = await shippingRateService.GetDefaultRateAsync(cancellationToken);
        DefaultRate = new ShippingRateInput { FirstItemAmount = rate.FirstItemAmount, AdditionalItemAmount = rate.AdditionalItemAmount };
    }
}
