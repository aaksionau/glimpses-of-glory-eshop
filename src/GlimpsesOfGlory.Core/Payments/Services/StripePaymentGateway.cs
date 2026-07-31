using GlimpsesOfGlory.Abstractions.Payments;
using Stripe;

namespace GlimpsesOfGlory.Core.Payments.Services;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly PaymentIntentService _paymentIntentService;
    private readonly string _webhookSecret;

    public StripePaymentGateway(string secretKey, string webhookSecret)
    {
        _paymentIntentService = new PaymentIntentService(new StripeClient(secretKey));
        _webhookSecret = webhookSecret;
    }

    public async Task<PaymentIntentSetup> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string? receiptEmail,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = ToSmallestCurrencyUnit(amount),
            Currency = currency,
            ReceiptEmail = receiptEmail,
            Metadata = metadata is null ? null : new Dictionary<string, string>(metadata),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        };

        var paymentIntent = await _paymentIntentService.CreateAsync(options, cancellationToken: cancellationToken);
        return new PaymentIntentSetup(paymentIntent.Id, paymentIntent.ClientSecret);
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

    private static long ToSmallestCurrencyUnit(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
