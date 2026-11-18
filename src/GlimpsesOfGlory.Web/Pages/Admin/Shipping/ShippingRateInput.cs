using System.ComponentModel.DataAnnotations;

namespace GlimpsesOfGlory.Web.Pages.Admin.Shipping;

public sealed class ShippingRateInput
{
    [Range(0, 100000, ErrorMessage = "Amount can't be negative.")]
    public decimal FirstItemAmount { get; set; }

    [Range(0, 100000, ErrorMessage = "Amount can't be negative.")]
    public decimal AdditionalItemAmount { get; set; }
}
