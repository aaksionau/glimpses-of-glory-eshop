namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderShippedView(
    int OrderId,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber);
