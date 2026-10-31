using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.AbandonedCheckouts;

// One table row; ErrorMessage is shown inline after a failed reminder send.
public sealed record AbandonedCheckoutRowViewModel(AbandonedCheckout Checkout, DateTimeOffset Now, string? ErrorMessage = null);

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

    // htmx post that swaps just this checkout's row: "Reminded {date}" once sent, or the button
    // again with an inline error so the admin can retry.
    public async Task<IActionResult> OnPostSendReminderAsync(int id, CancellationToken cancellationToken)
    {
        var siteBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await abandonedCheckoutService.SendReminderAsync(id, siteBaseUrl, cancellationToken);
        var checkout = await abandonedCheckoutService.GetAbandonedCheckoutAsync(id, cancellationToken);
        if (result == ReminderSendResult.NotFound || checkout is null)
        {
            return NotFound();
        }

        var errorMessage = result == ReminderSendResult.Failed
            ? "The reminder couldn't be sent. Please try again later."
            : null;
        return Partial("_AbandonedCheckoutRow", new AbandonedCheckoutRowViewModel(checkout, timeProvider.GetUtcNow(), errorMessage));
    }
}
