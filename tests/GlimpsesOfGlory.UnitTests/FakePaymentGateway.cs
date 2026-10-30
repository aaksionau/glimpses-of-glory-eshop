using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.UnitTests;

// IPaymentGateway double for code that only builds dashboard links; the payment calls throw.
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentIntentSetup> CreatePaymentIntentAsync(decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<PaymentIntentSetup> UpdatePaymentIntentAsync(string paymentIntentId, decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader) =>
        throw new NotSupportedException();

    public string GetPaymentDashboardUrl(string paymentIntentId) => $"https://dashboard.example/{paymentIntentId}";
}
