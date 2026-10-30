namespace GlimpsesOfGlory.Abstractions.Dtos;

// OrderId is null when the order couldn't be placed; UnavailableLines then says which cart lines
// ran short of stock (empty when the cause isn't line-specific, e.g. an empty cart).
public sealed record CashOrderResult(int? OrderId, IReadOnlyList<UnavailableCartLine> UnavailableLines)
{
    public static CashOrderResult Placed(int orderId) => new(orderId, []);

    public static CashOrderResult Failed(IReadOnlyList<UnavailableCartLine> unavailableLines) => new(null, unavailableLines);
}
