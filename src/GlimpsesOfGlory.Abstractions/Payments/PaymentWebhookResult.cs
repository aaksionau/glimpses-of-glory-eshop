namespace GlimpsesOfGlory.Abstractions.Payments;

public enum PaymentEventOutcome
{
    Succeeded,
    Failed,
    Irrelevant,
}

public sealed record PaymentWebhookResult(PaymentEventOutcome Outcome, string? PaymentIntentId, string? FailureMessage);
