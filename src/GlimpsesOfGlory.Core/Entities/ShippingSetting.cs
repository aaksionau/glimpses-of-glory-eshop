namespace GlimpsesOfGlory.Core.Entities;

// The store's default shipping rate (a single row), used for products without their own rate.
public sealed class ShippingSetting
{
    public const int DefaultId = 1;

    public int Id { get; set; }
    public decimal FirstItemAmount { get; set; }
    public decimal AdditionalItemAmount { get; set; }
}
