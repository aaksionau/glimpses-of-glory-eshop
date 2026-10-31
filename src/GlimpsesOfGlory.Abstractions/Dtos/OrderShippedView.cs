namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderShippedView(
    int OrderId,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber,
    // Null for "Other" (and unknown) carriers, whose tracking number is shown as plain text.
    string? TrackingUrl);
