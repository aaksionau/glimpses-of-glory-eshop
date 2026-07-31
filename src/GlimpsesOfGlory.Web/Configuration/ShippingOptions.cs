namespace GlimpsesOfGlory.Web.Configuration;

public sealed class ShippingOptions
{
    public List<ShippingTierOption> Tiers { get; set; } = [];
}

public sealed class ShippingTierOption
{
    public int MinQuantity { get; set; }
    public decimal Amount { get; set; }
}
