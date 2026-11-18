using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Pages.Admin.Shipping;

// A shipping rate as edited in admin forms: the store default, and a product's own rate.
public sealed class ShippingRateInput
{
    [Range(0, 100000, ErrorMessage = "Amount can't be negative.")]
    public decimal FirstItemAmount { get; set; }

    [Range(0, 100000, ErrorMessage = "Amount can't be negative.")]
    public decimal AdditionalItemAmount { get; set; }

    public static ShippingRateInput From(ShippingRate rate) =>
        new() { FirstItemAmount = rate.FirstItemAmount, AdditionalItemAmount = rate.AdditionalItemAmount };

    public ShippingRate ToRate() => new(FirstItemAmount, AdditionalItemAmount);
}
