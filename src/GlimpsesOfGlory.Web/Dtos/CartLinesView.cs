using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed record CartLineError(string ProductSlug, string Message);

// LineErrors holds at most one message per line, shown under that line. RemovedLine is the line
// the shopper just removed, offered back with an Undo; Message is a cart-wide error (an Undo that
// couldn't put the line back).
public sealed record CartLinesView(
    CartSummary Cart,
    IReadOnlyList<CartLineError> LineErrors,
    CartLineView? RemovedLine = null,
    string? Message = null)
{
    public CartLineError? ErrorFor(string productSlug) =>
        LineErrors.FirstOrDefault(e => e.ProductSlug == productSlug);
}
