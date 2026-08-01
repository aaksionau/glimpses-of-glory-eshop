using GlimpsesOfGlory.Abstractions.Cart;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed record CartLineError(string ProductSlug, string Message);

public sealed record CartLinesView(CartSummary Cart, CartLineError? QuantityError);
