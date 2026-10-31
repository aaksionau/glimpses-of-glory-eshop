using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record AdminOrderDetail(
    int Id,
    FulfillmentMethod FulfillmentMethod,
    PaymentMethod PaymentMethod,
    bool IsPaid,
    string? PaymentDashboardUrl,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    OrderStatus Status,
    string? TrackingNumber,
    // Null when there is no tracking number, or for orders shipped before carriers were recorded.
    ShippingCarrier? Carrier,
    // The carrier's tracking page; null for "Other" (and unknown) carriers, which show plain text.
    string? TrackingUrl,
    // Admin-only; null when there is no note.
    string? InternalNote,
    OrderTimestamps Timestamps,
    AdminOrderActions Actions);
