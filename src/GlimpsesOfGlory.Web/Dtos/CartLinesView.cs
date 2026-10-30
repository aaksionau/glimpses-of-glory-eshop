using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed record CartLineError(string ProductSlug, string Message);

// LineErrors holds at most one message per line, shown under that line.
public sealed record CartLinesView(CartSummary Cart, IReadOnlyList<CartLineError> LineErrors)
{
    public CartLineError? ErrorFor(string productSlug) =>
        LineErrors.FirstOrDefault(e => e.ProductSlug == productSlug);
}
