namespace GlimpsesOfGlory.Abstractions.Dtos;

// A cart line checkout couldn't reserve: AvailableQuantity is how many are left to buy now
// (0 when the product is gone or sold out).
public sealed record UnavailableCartLine(string ProductSlug, int AvailableQuantity);
