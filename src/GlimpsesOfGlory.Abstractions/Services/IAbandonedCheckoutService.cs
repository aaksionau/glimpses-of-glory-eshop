using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAbandonedCheckoutService
{
    // Pending Checkouts created more than an hour ago and less than 30 days ago, newest first,
    // leaving out any whose email has placed an Order since.
    Task<IReadOnlyList<AbandonedCheckout>> GetAbandonedCheckoutsAsync(CancellationToken cancellationToken);

    // One Pending Checkout by id, regardless of age, for re-rendering its row; null if it no longer exists.
    Task<AbandonedCheckout?> GetAbandonedCheckoutAsync(int id, CancellationToken cancellationToken);

    // Emails the shopper a one-time link back to their cart at {siteBaseUrl}/cart/restore/{token}.
    // RemindedAt and the restore token are saved only once the email has gone out.
    Task<ReminderSendResult> SendReminderAsync(int id, string siteBaseUrl, CancellationToken cancellationToken);
}
