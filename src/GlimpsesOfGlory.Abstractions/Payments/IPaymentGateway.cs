namespace GlimpsesOfGlory.Abstractions.Payments;

public interface IPaymentGateway
{
    Task<PaymentIntentSetup> CreatePaymentIntentAsync(
        decimal amount,
        string? receiptEmail,
        CancellationToken cancellationToken);

    // Throws PaymentIntentUnavailableException when the PaymentIntent can no longer be
    // updated (already succeeded, canceled, or expired) - callers should fall back to
    // CreatePaymentIntentAsync in that case.
    Task<PaymentIntentSetup> UpdatePaymentIntentAsync(
        string paymentIntentId,
        decimal amount,
        string? receiptEmail,
        CancellationToken cancellationToken);

    // Throws PaymentSignatureVerificationException when the signature doesn't verify.
    PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader);
}
