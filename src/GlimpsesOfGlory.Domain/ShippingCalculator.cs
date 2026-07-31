namespace GlimpsesOfGlory.Domain;

// Pure domain service: tiers are supplied by the caller (config/DB-backed),
// no I/O happens here.
public sealed class ShippingCalculator
{
    private readonly IReadOnlyList<ShippingTier> tiersDescending;

    public ShippingCalculator(IReadOnlyList<ShippingTier> tiers)
    {
        if (tiers.Count == 0)
        {
            throw new ArgumentException("At least one shipping tier is required.", nameof(tiers));
        }

        tiersDescending = [.. tiers.OrderByDescending(t => t.MinQuantity)];
    }

    public decimal Calculate(int totalQuantity)
    {
        if (totalQuantity <= 0)
        {
            return 0m;
        }

        var tier = tiersDescending.FirstOrDefault(t => totalQuantity >= t.MinQuantity) ?? tiersDescending[^1];
        return tier.Amount;
    }
}
