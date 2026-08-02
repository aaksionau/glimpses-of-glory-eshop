namespace GlimpsesOfGlory.Abstractions.Orders;

public sealed record AdminOrderDetail(
    int Id,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    OrderStatus Status,
    string? TrackingNumber,
    DateTimeOffset CreatedAt);
