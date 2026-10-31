using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

// PaymentIntentId is the restored Pending Checkout's, so a later checkout updates it instead of creating another.
public sealed record CartRestoreResult(CartRestoreStatus Status, string? PaymentIntentId, int SkippedLineCount)
{
    public static CartRestoreResult Restored(string paymentIntentId, int skippedLineCount) => new(CartRestoreStatus.Restored, paymentIntentId, skippedLineCount);

    public static CartRestoreResult Expired() => new(CartRestoreStatus.Expired, null, 0);
}
