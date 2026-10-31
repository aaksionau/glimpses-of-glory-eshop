using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAbandonedCheckoutService
{
    // Pending Checkouts created more than an hour ago and less than 30 days ago, newest first,
    // leaving out any whose email has placed an Order since.
    Task<IReadOnlyList<AbandonedCheckout>> GetAbandonedCheckoutsAsync(CancellationToken cancellationToken);
}
