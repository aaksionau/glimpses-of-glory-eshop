using GlimpsesOfGlory.Core.Orders.ValueObjects;

namespace GlimpsesOfGlory.Core.Orders.Entities;

public sealed class Order : ICheckoutHeader
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required ShippingAddress ShippingAddress { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total => Subtotal + ShippingCost;
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public required string StripePaymentIntentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<OrderLine> Lines { get; set; } = [];
}
