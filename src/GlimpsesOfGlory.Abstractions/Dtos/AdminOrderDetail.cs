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
    // Admin-only; null when there is no note.
    string? InternalNote,
    DateTimeOffset CreatedAt,
    // Null when the step hasn't happened - or happened before step timestamps were recorded.
    DateTimeOffset? PaidAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? PickedUpAt,
    DateTimeOffset? CancelledAt,
    bool CanMarkShipped,
    bool CanMarkPickedUp,
    bool CanMarkPaid,
    bool CanMarkCancelled,
    bool NeedsRefund);
