using GlimpsesOfGlory.Core.Orders.ValueObjects;

namespace GlimpsesOfGlory.Core.Orders.Entities;

// A cart+address snapshot held between "PaymentIntent created" and "Stripe webhook
// confirms it". The webhook has no access to the shopper's session, so this is the
// durable link between the PaymentIntent and what to persist as an Order once
// payment succeeds. Consumed (deleted) by OrderService.ConfirmPaymentAsync.
public sealed class PendingCheckout
{
    public int Id { get; set; }
    public required string StripePaymentIntentId { get; set; }
    public required string Email { get; set; }
    public required ShippingAddress ShippingAddress { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PendingCheckoutLine> Lines { get; set; } = [];
}
