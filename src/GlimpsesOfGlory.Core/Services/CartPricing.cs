using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Core.Services;

// Priced is null when the cart is empty or can't be fulfilled as it stands; UnavailableLines then
// lists the lines that are short (none for an empty cart).
public sealed record CartPricing(PricedCart? Priced, IReadOnlyList<UnavailableCartLine> UnavailableLines);
