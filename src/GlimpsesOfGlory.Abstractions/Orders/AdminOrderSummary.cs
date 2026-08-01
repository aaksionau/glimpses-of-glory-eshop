namespace GlimpsesOfGlory.Abstractions.Orders;

public sealed record AdminOrderSummary(
    int Id,
    string Email,
    int ItemCount,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
