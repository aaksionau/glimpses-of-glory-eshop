namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record ProductEditRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate,
    // Null: the product ships at the store default rate.
    ShippingRate? ShippingRate);
