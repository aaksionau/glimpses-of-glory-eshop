using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Exceptions;
using GlimpsesOfGlory.Abstractions.Services;
using Stripe;

namespace GlimpsesOfGlory.Core.Services;

public sealed class StripePaymentGateway : IPaymentGateway
{
    // The store only sells in USD; there's no per-order currency selection to plumb through.
    private const string Currency = "usd";

    private readonly PaymentIntentService _paymentIntentService;
    private readonly string _webhookSecret;
    private readonly bool _isTestMode;

    public StripePaymentGateway(string secretKey, string webhookSecret)
    {
        _paymentIntentService = new PaymentIntentService(new StripeClient(secretKey));
        _webhookSecret = webhookSecret;
        // Test-mode objects live under a separate path in the Stripe dashboard.
        _isTestMode = secretKey.StartsWith("sk_test_", StringComparison.Ordinal) || secretKey.StartsWith("rk_test_", StringComparison.Ordinal);
    }

    public async Task<PaymentIntentSetup> CreatePaymentIntentAsync(
        decimal amount,
        string? receiptEmail,
        CancellationToken cancellationToken)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = ToSmallestCurrencyUnit(amount),
            Currency = Currency,
            ReceiptEmail = receiptEmail,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        };

        var paymentIntent = await _paymentIntentService.CreateAsync(options, cancellationToken: cancellationToken);
        return new PaymentIntentSetup(paymentIntent.Id, paymentIntent.ClientSecret, amount);
    }

    public async Task<PaymentIntentSetup> UpdatePaymentIntentAsync(
        string paymentIntentId,
        decimal amount,
        string? receiptEmail,
        CancellationToken cancellationToken)
    {
        var options = new PaymentIntentUpdateOptions
        {
            Amount = ToSmallestCurrencyUnit(amount),
            ReceiptEmail = receiptEmail,
        };

        try
        {
            var paymentIntent = await _paymentIntentService.UpdateAsync(paymentIntentId, options, cancellationToken: cancellationToken);
            return new PaymentIntentSetup(paymentIntent.Id, paymentIntent.ClientSecret, amount);
        }
        catch (StripeException ex)
        {
            throw new PaymentIntentUnavailableException($"PaymentIntent '{paymentIntentId}' can no longer be updated.", ex);
        }
    }

    public PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader)
    {
        Event stripeEvent;
        try
        {
            // The API version on incoming webhook events is whatever the Stripe dashboard's
            // webhook endpoint is configured with, not necessarily what this Stripe.net
            // version was built against, so don't fail signature verification over a mismatch.
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new PaymentSignatureVerificationException("Stripe webhook signature verification failed.", ex);
        }

        return stripeEvent.Type switch
        {
            EventTypes.PaymentIntentSucceeded => new PaymentWebhookResult(
                PaymentEventOutcome.Succeeded,
                (stripeEvent.Data.Object as PaymentIntent)?.Id,
                null),

            EventTypes.PaymentIntentPaymentFailed => new PaymentWebhookResult(
                PaymentEventOutcome.Failed,
                (stripeEvent.Data.Object as PaymentIntent)?.Id,
                (stripeEvent.Data.Object as PaymentIntent)?.LastPaymentError?.Message),

            _ => new PaymentWebhookResult(PaymentEventOutcome.Irrelevant, null, null),
        };
    }

    public string GetPaymentDashboardUrl(string paymentIntentId) =>
        $"https://dashboard.stripe.com/{(_isTestMode ? "test/" : "")}payments/{Uri.EscapeDataString(paymentIntentId)}";

    private static long ToSmallestCurrencyUnit(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
