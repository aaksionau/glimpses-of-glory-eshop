namespace GlimpsesOfGlory.Abstractions.Dtos;

// What shipping one product costs: FirstItemAmount when it's the item that opens the parcel,
// AdditionalItemAmount for every other unit of it. Used for a product's own rate and the store default.
public sealed record ShippingRate(decimal FirstItemAmount, decimal AdditionalItemAmount);
