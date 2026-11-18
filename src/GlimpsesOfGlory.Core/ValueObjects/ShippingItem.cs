using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Core.ValueObjects;

public sealed record ShippingItem(ShippingRate Rate, int Quantity);
