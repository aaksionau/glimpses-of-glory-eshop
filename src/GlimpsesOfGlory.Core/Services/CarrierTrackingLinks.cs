using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Core.Services;

// Single source of the carrier tracking-page URLs, so the admin order page and the buyer's
// shipping email link to the same place. Null means "show the tracking number as plain text".
public static class CarrierTrackingLinks
{
    public static string? TrackingUrl(ShippingCarrier? carrier, string? trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return null;
        }

        var number = Uri.EscapeDataString(trackingNumber.Trim());
        return carrier switch
        {
            ShippingCarrier.Usps => $"https://tools.usps.com/go/TrackConfirmAction?tLabels={number}",
            _ => null,
        };
    }
}
