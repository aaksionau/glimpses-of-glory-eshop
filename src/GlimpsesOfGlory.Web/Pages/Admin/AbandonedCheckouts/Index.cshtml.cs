using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.AbandonedCheckouts;

public class IndexModel(IAbandonedCheckoutService abandonedCheckoutService, TimeProvider timeProvider) : PageModel
{
    // The page's "now" for relative ages, from the same clock the service filters with.
    public DateTimeOffset Now { get; private set; }

    public IReadOnlyList<AbandonedCheckout> Checkouts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Now = timeProvider.GetUtcNow();
        Checkouts = await abandonedCheckoutService.GetAbandonedCheckoutsAsync(cancellationToken);
    }
}
