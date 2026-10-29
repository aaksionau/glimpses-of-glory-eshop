using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record AdminOrderSummary(
    int Id,
    string Email,
    int ItemCount,
    decimal Total,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    PaymentMethod PaymentMethod,
    bool IsPaid,
    bool HasPreorderLines,
    DateTimeOffset CreatedAt);
