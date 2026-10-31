using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Formatting;

public static class CarrierLabels
{
    // The carriers offered on the Mark shipped form, in display order.
    public static IReadOnlyList<ShippingCarrier> All { get; } = [ShippingCarrier.Usps, ShippingCarrier.Other];

    public static string Name(ShippingCarrier carrier) => carrier switch
    {
        ShippingCarrier.Usps => "USPS",
        _ => "Other",
    };
}
