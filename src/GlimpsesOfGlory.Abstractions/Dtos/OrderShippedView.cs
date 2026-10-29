using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderShippedView(
    int OrderId,
    FulfillmentMethod FulfillmentMethod,
    PaymentMethod PaymentMethod,
    bool IsPaid,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total,
    string? TrackingNumber);
