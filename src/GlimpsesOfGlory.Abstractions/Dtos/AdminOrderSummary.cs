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
    // The soonest expected availability date across the order's preorder lines, if any carry one.
    DateOnly? EarliestExpectedAvailability,
    bool NeedsRefund,
    bool NeedsAction,
    bool HasInternalNote,
    DateTimeOffset CreatedAt);
