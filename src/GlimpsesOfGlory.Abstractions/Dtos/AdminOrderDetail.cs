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
    DateTimeOffset CreatedAt,
    bool CanMarkShipped,
    bool CanMarkPaid,
    bool CanMarkCancelled,
    bool NeedsRefund);
