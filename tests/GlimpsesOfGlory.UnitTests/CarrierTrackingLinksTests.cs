using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

public class CarrierTrackingLinksTests
{
    [Fact]
    public void Usps_LinksToTheUspsTrackingPage()
    {
        Assert.Equal(
            "https://tools.usps.com/go/TrackConfirmAction?tLabels=9400111899223",
            CarrierTrackingLinks.TrackingUrl(ShippingCarrier.Usps, " 9400111899223 "));
    }

    [Fact]
    public void Usps_EscapesTheTrackingNumber()
    {
        Assert.Equal(
            "https://tools.usps.com/go/TrackConfirmAction?tLabels=94%2026%26x%3D1",
            CarrierTrackingLinks.TrackingUrl(ShippingCarrier.Usps, "94 26&x=1"));
    }

    [Theory]
    [InlineData(ShippingCarrier.Other, "ABC123")]
    [InlineData(null, "ABC123")]
    [InlineData(ShippingCarrier.Usps, null)]
    [InlineData(ShippingCarrier.Usps, "  ")]
    public void OtherOrUnknownCarrier_OrNoTrackingNumber_HasNoLink(ShippingCarrier? carrier, string? trackingNumber)
    {
        Assert.Null(CarrierTrackingLinks.TrackingUrl(carrier, trackingNumber));
    }
}
