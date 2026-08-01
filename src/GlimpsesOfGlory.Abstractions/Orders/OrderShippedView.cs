namespace GlimpsesOfGlory.Abstractions.Orders;

public sealed record OrderShippedView(
    int OrderId,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber);
