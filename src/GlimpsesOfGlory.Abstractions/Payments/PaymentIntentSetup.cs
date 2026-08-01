namespace GlimpsesOfGlory.Abstractions.Payments;

public sealed record PaymentIntentSetup(string PaymentIntentId, string ClientSecret);
