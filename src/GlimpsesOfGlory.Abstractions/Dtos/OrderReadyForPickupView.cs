namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record OrderReadyForPickupView(
    int OrderId,
    bool IsPaid,
    ShippingAddressInfo Address,
    IReadOnlyList<OrderConfirmationLine> Lines,
    decimal Total);
