using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Dtos;

// A Pending Checkout the shopper left at the card-payment step, as listed in admin.
public sealed record AbandonedCheckout(
    int Id,
    string Email,
    IReadOnlyList<AbandonedCheckoutLine> Lines,
    decimal Total,
    FulfillmentMethod FulfillmentMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RemindedAt);

public sealed record AbandonedCheckoutLine(string ProductName, int Quantity);
