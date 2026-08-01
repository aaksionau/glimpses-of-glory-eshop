using System.ComponentModel.DataAnnotations;

namespace GlimpsesOfGlory.Web.Pages.Admin.Shipping;

public sealed class ShippingTierInput
{
    public int Id { get; set; }

    [Range(0, 100000, ErrorMessage = "Minimum quantity can't be negative.")]
    public int MinQuantity { get; set; }

    [Range(0, 100000, ErrorMessage = "Amount can't be negative.")]
    public decimal Amount { get; set; }
}
