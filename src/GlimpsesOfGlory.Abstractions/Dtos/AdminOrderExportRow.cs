using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

// One order as it appears in the bookkeeping CSV export.
public sealed record AdminOrderExportRow(
    int Id,
    DateTimeOffset CreatedAt,
    string CustomerName,
    string Email,
    FulfillmentMethod FulfillmentMethod,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    DateTimeOffset? PaidAt,
    decimal Subtotal,
    decimal ShippingCost)
{
    public decimal Total => Subtotal + ShippingCost;
}
