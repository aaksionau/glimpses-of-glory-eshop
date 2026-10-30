namespace GlimpsesOfGlory.Abstractions.Dtos;

// A PaymentIntent ready for the card form, plus the cart as it was priced for it: the lines carry
// catalogue prices (not the session cart's add-time prices), so Cart.TotalFor(the chosen method)
// is exactly what the PaymentIntent charges.
public sealed record CheckoutPaymentSetup(string PaymentIntentId, string ClientSecret, CartSummary Cart);
