namespace GlimpsesOfGlory.Abstractions.Dtos;

// Amount is what the PaymentIntent will charge, in the store currency.
public sealed record PaymentIntentSetup(string PaymentIntentId, string ClientSecret, decimal Amount);
