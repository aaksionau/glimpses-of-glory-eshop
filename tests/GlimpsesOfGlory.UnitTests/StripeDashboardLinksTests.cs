using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class StripeDashboardLinksTests
{
    [Theory]
    [InlineData(false, "https://dashboard.stripe.com/payments/pi_123")]
    [InlineData(true, "https://dashboard.stripe.com/test/payments/pi_123")]
    public void Payment_PointsAtLiveOrTestDashboard(bool testMode, string expected)
    {
        Assert.Equal(expected, StripeDashboardLinks.Payment("pi_123", testMode));
    }

    [Theory]
    [InlineData("sk_test_abc", true)]
    [InlineData("rk_test_abc", true)]
    [InlineData("sk_live_abc", false)]
    [InlineData("", false)]
    public void IsTestMode_FollowsSecretKeyPrefix(string secretKey, bool expected)
    {
        Assert.Equal(expected, new StripeOptions { SecretKey = secretKey }.IsTestMode);
    }
}
