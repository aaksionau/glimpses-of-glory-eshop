using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record AdminOrderSummary(
    int Id,
    string CustomerName,
    string Email,
    int ItemCount,
    decimal Total,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    bool IsPaid,
    bool HasPreorderLines,
    bool NeedsRefund,
    bool NeedsAction,
    DateTimeOffset CreatedAt);
