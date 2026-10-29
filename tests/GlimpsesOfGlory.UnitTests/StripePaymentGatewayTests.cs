using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

public class StripePaymentGatewayTests
{
    [Theory]
    [InlineData("sk_live_abc", "https://dashboard.stripe.com/payments/pi_123")]
    [InlineData("sk_test_abc", "https://dashboard.stripe.com/test/payments/pi_123")]
    [InlineData("rk_test_abc", "https://dashboard.stripe.com/test/payments/pi_123")]
    public void GetPaymentDashboardUrl_PointsAtLiveOrTestDashboardBySecretKey(string secretKey, string expected)
    {
        var gateway = new StripePaymentGateway(secretKey, "whsec_abc");

        Assert.Equal(expected, gateway.GetPaymentDashboardUrl("pi_123"));
    }
}
