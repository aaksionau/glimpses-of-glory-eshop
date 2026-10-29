namespace GlimpsesOfGlory.Web.Helpers;

public static class StripeDashboardLinks
{
    public static string Payment(string paymentIntentId, bool testMode) =>
        $"https://dashboard.stripe.com/{(testMode ? "test/" : "")}payments/{Uri.EscapeDataString(paymentIntentId)}";
}
