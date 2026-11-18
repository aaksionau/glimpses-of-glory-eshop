namespace GlimpsesOfGlory.Core.ValueObjects;

// What shipping one product costs: FirstItem when it's the item that opens the parcel,
// AdditionalItem for every other unit of it.
public sealed record ShippingRate(decimal FirstItem, decimal AdditionalItem);

public sealed record ShippingItem(ShippingRate Rate, int Quantity);
