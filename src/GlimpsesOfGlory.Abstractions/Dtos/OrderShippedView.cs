using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderShippedView(
    int OrderId,
    FulfillmentMethod FulfillmentMethod,
    bool IsPaid,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber,
    // Null for "Other" (and unknown) carriers, whose tracking number is shown as plain text.
    string? TrackingUrl);
