using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Exceptions;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.IntegrationTests;

// IPaymentGateway double for the Stripe webhook: a request signed with ValidSignature yields
// NextWebhookResult, any other signature fails verification the way StripePaymentGateway does.
internal sealed class WebhookPaymentGateway : IPaymentGateway
{
    public const string ValidSignature = "t=1,v1=valid";

    public PaymentWebhookResult NextWebhookResult { get; set; } = new(PaymentEventOutcome.Irrelevant, null, null);

    public Task<PaymentIntentSetup> CreatePaymentIntentAsync(decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<PaymentIntentSetup> UpdatePaymentIntentAsync(string paymentIntentId, decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader) =>
        signatureHeader == ValidSignature
            ? NextWebhookResult
            : throw new PaymentSignatureVerificationException("Signature mismatch", new InvalidOperationException("Bad signature"));

    public string GetPaymentDashboardUrl(string paymentIntentId) => $"https://dashboard.example/{paymentIntentId}";
}
