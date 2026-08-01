namespace GlimpsesOfGlory.Abstractions.Payments;

// Thrown by IPaymentGateway.UpdatePaymentIntentAsync when the PaymentIntent can no
// longer be updated (e.g. already succeeded, canceled, or expired). Callers should
// treat this as a signal to create a fresh PaymentIntent instead.
public sealed class PaymentIntentUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
