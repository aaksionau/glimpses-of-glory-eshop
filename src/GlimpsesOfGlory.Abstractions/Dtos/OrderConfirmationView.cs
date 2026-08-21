namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderConfirmationLine(
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate);

public sealed record OrderConfirmationView(
    int OrderId,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    DateTimeOffset CreatedAt);
