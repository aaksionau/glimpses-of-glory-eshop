namespace GlimpsesOfGlory.Abstractions.Orders;

public sealed record OrderConfirmationLine(string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderConfirmationView(
    int OrderId,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    DateTimeOffset CreatedAt);
