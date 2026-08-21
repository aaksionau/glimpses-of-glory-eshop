using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed record CartLineError(string ProductSlug, string Message);

public sealed record CartLinesView(CartSummary Cart, CartLineError? QuantityError);
