namespace GlimpsesOfGlory.Abstractions.Dtos;

// Setup is null when payment couldn't be set up; UnavailableLines then says which cart lines
// ran short of stock (empty when the cause isn't line-specific, e.g. an empty cart).
public sealed record CheckoutPaymentResult(CheckoutPaymentSetup? Setup, IReadOnlyList<UnavailableCartLine> UnavailableLines)
{
    public static CheckoutPaymentResult Ready(CheckoutPaymentSetup setup) => new(setup, []);

    public static CheckoutPaymentResult Failed(IReadOnlyList<UnavailableCartLine> unavailableLines) => new(null, unavailableLines);
}
