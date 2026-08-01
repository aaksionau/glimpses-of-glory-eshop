namespace GlimpsesOfGlory.Abstractions.Payments;

// Thrown by IPaymentGateway.HandleWebhookEvent when the webhook signature doesn't
// verify against the configured secret, so callers can reject the request (400)
// without needing to know about the underlying payment SDK's exception types.
public sealed class PaymentSignatureVerificationException(string message, Exception innerException)
    : Exception(message, innerException);
