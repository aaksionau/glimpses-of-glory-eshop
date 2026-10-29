using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderConfirmationLine(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate);

public sealed record OrderConfirmationView(
    int OrderId,
    FulfillmentMethod FulfillmentMethod,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    DateTimeOffset CreatedAt);
