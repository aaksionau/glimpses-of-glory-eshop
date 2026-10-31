namespace GlimpsesOfGlory.Abstractions.Dtos;

// PaymentIntentId is the restored Pending Checkout's, so a later checkout updates it instead of creating another.
// It is null when the link has expired: the token matches no Pending Checkout, because it was never issued or
// the checkout has since been paid or removed.
public sealed record CartRestoreResult(string? PaymentIntentId, int SkippedLineCount)
{
    public static CartRestoreResult Restored(string paymentIntentId, int skippedLineCount) => new(paymentIntentId, skippedLineCount);

    public static CartRestoreResult Expired() => new(null, 0);
}
