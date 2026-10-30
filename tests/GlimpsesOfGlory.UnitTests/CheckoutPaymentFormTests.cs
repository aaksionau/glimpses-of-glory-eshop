using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutPaymentFormTests
{
    private const string PartialPath = "~/Pages/Checkout/_PaymentForm.cshtml";

    private static readonly CartSummary Cart = new(
        [new CartLineView("sunrise", "Sunrise over the lake", "sunrise.jpg", 20m, 2, false, null)],
        Subtotal: 40m,
        ShippingCost: 8m);

    [Fact]
    public async Task PayButton_ShowsTheAmount_AndStartsDisabledAndNotBusy_UntilThePaymentFormIsReady()
    {
        var html = await RenderAsync();
        var button = Tag(html, "submit-payment");

        Assert.Contains(" disabled", button);
        Assert.Contains("aria-busy=\"false\"", button);
        Assert.Matches("<span data-idle>Pay \\$48\\.00</span>", html);
    }

    [Fact]
    public async Task PayButton_HasAHiddenSpinnerAndProcessingLabel_ForWhileThePaymentConfirms()
    {
        var button = ButtonContent(await RenderAsync());

        Assert.Matches("<svg data-processing hidden[^>]*animate-spin[^>]*aria-hidden=\"true\"", button);
        Assert.Contains($"<span data-processing hidden>{CheckoutMessages.PaymentProcessing}</span>", WebUtility.HtmlDecode(button));
    }

    [Fact]
    public async Task ErrorBox_IsAFocusableLiveRegion_AboveThePaymentElement()
    {
        var html = await RenderAsync();
        var errorBox = Tag(html, "payment-error");

        Assert.Contains("role=\"alert\"", errorBox);
        Assert.Contains("tabindex=\"-1\"", errorBox);
        Assert.True(html.IndexOf("id=\"payment-error\"", StringComparison.Ordinal) < html.IndexOf("id=\"payment-element\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Form_CarriesTheStripeKeys_AndTheMessagesTheScriptShows()
    {
        var form = WebUtility.HtmlDecode(Tag(await RenderAsync(), "payment-form"));

        Assert.Contains("data-publishable-key=\"pk_test_123\"", form);
        Assert.Contains("data-client-secret=\"pi_1_secret_abc\"", form);
        Assert.Contains($"data-load-error=\"{CheckoutMessages.PaymentFormUnavailable}\"", form);
        Assert.Contains($"data-payment-error=\"{CheckoutMessages.PaymentFailed}\"", form);
        Assert.Equal("Payment form couldn't load. Refresh or try again later.", CheckoutMessages.PaymentFormUnavailable);
    }

    [Fact]
    public void PaymentScript_HandlesLoadFailures_ShowsProcessingState_AndSurfacesErrors()
    {
        var script = File.ReadAllText(WebRootFile("js", "checkout-payment.js"));

        // Stripe.js missing, and the Payment Element's own load failure.
        Assert.Contains("typeof window.Stripe !== 'function'", script);
        Assert.Contains("on('loaderror'", script);
        // Processing state during confirmation, reset on error.
        Assert.Contains("setAttribute('aria-busy'", script);
        Assert.Matches(@"if \(result\.error\) \{\s*setProcessing\(false\);", script);
        // Errors are brought into view and focused.
        Assert.Contains("scrollIntoView(", script);
        Assert.Contains("errorBox.focus(", script);
        Assert.DoesNotContain("jQuery", script);
    }

    [Fact]
    public void PaymentPage_UsesThePaymentFormPartial_AndScript_WithNoInlineStripeCode()
    {
        var page = File.ReadAllText(Path.Combine(WebProjectDirectory(), "Pages", "Checkout", "Payment.cshtml"));

        Assert.Contains("<partial name=\"_PaymentForm\"", page);
        Assert.Contains("~/js/checkout-payment.js", page);
        Assert.DoesNotContain("confirmPayment", page);
    }

    private static async Task<string> RenderAsync()
    {
        var page = await CheckoutOrderSummaryTests.CreatePaymentPageAsync(
            new CheckoutPaymentSetup("pi_1", "pi_1_secret_abc", Cart), publishableKey: "pk_test_123");
        return await RazorViewToStringRendererTests.CreateRenderer().RenderAsync(PartialPath, page);
    }

    private static string Tag(string html, string id) =>
        Regex.Match(html, $"<[a-z]+\\b[^>]*id=\"{id}\"[^>]*>", RegexOptions.Singleline).Value;

    private static string ButtonContent(string html) =>
        Regex.Match(html, "<button\\b[^>]*id=\"submit-payment\"[^>]*>(.*?)</button>", RegexOptions.Singleline).Groups[1].Value;

    private static string WebRootFile(params string[] parts) =>
        Path.Combine([WebProjectDirectory(), "wwwroot", .. parts]);

    private static string WebProjectDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "GlimpsesOfGlory.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find src/GlimpsesOfGlory.Web above the test output directory.");
    }
}
