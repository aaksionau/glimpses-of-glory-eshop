using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.Core.Entities;

public sealed class Order : ICheckoutHeader
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public FulfillmentMethod FulfillmentMethod { get; set; }
    public required ShippingAddress ShippingAddress { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total => Subtotal + ShippingCost;
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public string? TrackingNumber { get; set; }
    // Recorded with the tracking number; null when there is none.
    public ShippingCarrier? Carrier { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    // Set when the payment is confirmed: on the Stripe webhook for card orders, or by an
    // admin once the cash has been collected for cash orders.
    public DateTimeOffset? PaidAt { get; set; }
    public bool IsPaid => PaidAt is not null;
    // Null for cash orders, which never go through Stripe.
    public string? StripePaymentIntentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    // Stamped once by AdminOrderService when the order reaches each step; null for orders that
    // never reached it, and for orders that reached it before these columns existed.
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? ReadyForPickupAt { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    // Private to admins: never copied into a customer email or shown on a customer page.
    public string? InternalNote { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}
