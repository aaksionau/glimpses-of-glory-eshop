namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record ProductEditRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate,
    ProductShippingRate? ShippingRate);

// A product's own shipping rate; a null rate means the product ships at the store default.
public sealed record ProductShippingRate(decimal FirstItemAmount, decimal AdditionalItemAmount);
