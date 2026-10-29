using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderShippedView(
    int OrderId,
    FulfillmentMethod FulfillmentMethod,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber);
