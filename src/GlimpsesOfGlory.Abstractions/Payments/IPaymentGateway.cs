namespace GlimpsesOfGlory.Abstractions.Payments;

public interface IPaymentGateway
{
    Task<PaymentIntentSetup> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string? receiptEmail,
        CancellationToken cancellationToken);

    // Throws PaymentSignatureVerificationException when the signature doesn't verify.
    PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader);
}
