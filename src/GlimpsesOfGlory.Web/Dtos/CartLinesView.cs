using GlimpsesOfGlory.Abstractions.Cart;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed record CartLinesView(CartSummary Cart, string? QuantityErrorSlug, string? QuantityErrorMessage);
