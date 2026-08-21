using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record PaymentWebhookResult(PaymentEventOutcome Outcome, string? PaymentIntentId, string? FailureMessage);
