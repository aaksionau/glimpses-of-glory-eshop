namespace GlimpsesOfGlory.Web.Configuration;

public sealed class StripeOptions
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;

    // Test-mode objects live under a separate path in the Stripe dashboard.
    public bool IsTestMode => SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) || SecretKey.StartsWith("rk_test_", StringComparison.Ordinal);
}
