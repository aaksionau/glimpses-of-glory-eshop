using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlimpsesOfGlory.Abstractions.Payments;
using GlimpsesOfGlory.Core.Payments.Services;

namespace GlimpsesOfGlory.UnitTests;

public class StripePaymentGatewayTests
{
    private const string WebhookSecret = "whsec_test_secret";

    private static StripePaymentGateway CreateGateway() => new("sk_test_dummy", WebhookSecret);

    // Hand-builds a Stripe event payload and a valid webhook signature header for it,
    // matching Stripe's "t=<timestamp>,v1=<hex hmac-sha256>" scheme, so tests never call
    // the real Stripe API.
    private static (string Payload, string Signature) BuildSignedEvent(string eventType, object dataObject)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_test",
            @object = "event",
            api_version = "2024-06-20",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            type = eventType,
            data = new { @object = dataObject },
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var signatureHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));

        return (payload, $"t={timestamp},v1={signatureHex}");
    }

    [Fact]
    public void HandleWebhookEvent_ReturnsSucceeded_ForPaymentIntentSucceededEvent()
    {
        var gateway = CreateGateway();
        var (payload, signature) = BuildSignedEvent("payment_intent.succeeded", new
        {
            id = "pi_123",
            @object = "payment_intent",
            amount = 4999,
            currency = "usd",
            status = "succeeded",
        });

        var result = gateway.HandleWebhookEvent(payload, signature);

        Assert.Equal(PaymentEventOutcome.Succeeded, result.Outcome);
        Assert.Equal("pi_123", result.PaymentIntentId);
    }

    [Fact]
    public void HandleWebhookEvent_ReturnsFailed_ForPaymentIntentPaymentFailedEvent()
    {
        var gateway = CreateGateway();
        var (payload, signature) = BuildSignedEvent("payment_intent.payment_failed", new
        {
            id = "pi_456",
            @object = "payment_intent",
            amount = 2500,
            currency = "usd",
            status = "requires_payment_method",
            last_payment_error = new { message = "Your card was declined." },
        });

        var result = gateway.HandleWebhookEvent(payload, signature);

        Assert.Equal(PaymentEventOutcome.Failed, result.Outcome);
        Assert.Equal("pi_456", result.PaymentIntentId);
        Assert.Equal("Your card was declined.", result.FailureMessage);
    }

    [Fact]
    public void HandleWebhookEvent_ReturnsIrrelevant_ForUnhandledEventType()
    {
        var gateway = CreateGateway();
        var (payload, signature) = BuildSignedEvent("charge.refunded", new
        {
            id = "ch_789",
            @object = "charge",
        });

        var result = gateway.HandleWebhookEvent(payload, signature);

        Assert.Equal(PaymentEventOutcome.Irrelevant, result.Outcome);
        Assert.Null(result.PaymentIntentId);
    }

    [Fact]
    public void HandleWebhookEvent_ThrowsSignatureVerificationException_ForInvalidSignature()
    {
        var gateway = CreateGateway();
        var (payload, _) = BuildSignedEvent("payment_intent.succeeded", new
        {
            id = "pi_123",
            @object = "payment_intent",
        });

        Assert.Throws<PaymentSignatureVerificationException>(
            () => gateway.HandleWebhookEvent(payload, "t=1,v1=deadbeef"));
    }
}
