using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Exceptions;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.Web.Endpoints;

// POST /webhooks/stripe: Stripe's payment events. A verified "succeeded" event turns the
// matching PendingCheckout into an Order; a bad signature is rejected with 400; every other
// verified event is acknowledged with 200 so Stripe doesn't retry it.
internal static class StripeWebhookEndpoint
{
    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/stripe", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IPaymentGateway paymentGateway,
        IOrderService orderService,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = request.Headers["Stripe-Signature"].ToString();

        PaymentWebhookResult result;
        try
        {
            result = paymentGateway.HandleWebhookEvent(payload, signature);
        }
        catch (PaymentSignatureVerificationException)
        {
            return Results.BadRequest();
        }

        if (result is { Outcome: PaymentEventOutcome.Succeeded, PaymentIntentId: not null })
        {
            await orderService.ConfirmPaymentAsync(result.PaymentIntentId, cancellationToken);
        }

        return Results.Ok();
    }
}
