namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record ProductEditRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsPreorder,
    DateOnly? ExpectedAvailabilityDate,
    // Null: the product ships at the store default rate.
    ShippingRate? ShippingRate,
    // The stock the edit form was loaded with. Orders move stock while the form is open, so on
    // update StockQuantity is only written when it differs from this, and only if the stock is
    // still this value. Null (a new product) writes StockQuantity as given.
    int? StockQuantityWhenLoaded = null);
