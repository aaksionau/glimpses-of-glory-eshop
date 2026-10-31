using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface ICartRestoreService
{
    // Merges the lines of the Pending Checkout behind a reminder's restore token into the current cart,
    // keeping the larger quantity per line. Only lines are restored, never the email or address.
    Task<CartRestoreResult> RestoreAsync(string token, CancellationToken cancellationToken);
}
