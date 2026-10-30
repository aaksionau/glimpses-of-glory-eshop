using System.Globalization;
using System.Text.RegularExpressions;

namespace GlimpsesOfGlory.UnitTests;

// Primary gold buttons (#98): every storefront CTA shares the `btn-primary` treatment, and its
// white label meets WCAG AA contrast (4.5:1) against the button at rest and on hover.
public class PrimaryButtonTests
{
    private const double WcagAaNormalText = 4.5;

    public static TheoryData<string, string> PrimaryCtas => new()
    {
        { "Index.cshtml", "Shop the Collection" },
        { "Products/Details.cshtml", "Add to cart" },
        { "Cart/_CartLines.cshtml", "Proceed to checkout" },
        { "Checkout/Index.cshtml", "Continue to review" },
        { "Checkout/Review.cshtml", "Continue to payment" },
        { "Checkout/_PickupPaymentChoice.cshtml", "Place order" },
        { "Checkout/_PaymentForm.cshtml", "submit-payment" },
        { "Checkout/_ConfirmationResult.cshtml", "Continue shopping" },
    };

    [Theory]
    [MemberData(nameof(PrimaryCtas))]
    public void PrimaryCta_UsesTheSharedButtonClass(string page, string marker)
    {
        var markup = File.ReadAllText(PagePath(page));
        var markerIndex = markup.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"'{marker}' not found in {page}");

        // The class attribute of the nearest <a>/<button> opening tag before the CTA's text (or id).
        var tagStart = Math.Max(
            markup.LastIndexOf("<a ", markerIndex, StringComparison.Ordinal),
            markup.LastIndexOf("<button", markerIndex, StringComparison.Ordinal));
        Assert.True(tagStart >= 0, $"No <a> or <button> before '{marker}' in {page}");
        var tag = markup[tagStart..markup.IndexOf('>', tagStart)];
        var classes = Regex.Match(tag, "class=\"([^\"]*)\"").Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("btn-primary", classes);
        // The look lives in btn-primary only; no page restyles its colours.
        Assert.DoesNotContain(classes, c => c.Contains("bg-") || c.Contains("text-white") || c.StartsWith("disabled:"));
    }

    [Fact]
    public void NoStorefrontPage_PutsWhiteTextOnPlainGold_ForAButton()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(CheckoutPaymentFormTests.WebProjectDirectory(), "Pages"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "<(?:a|button)\\b[^>]*class=\"([^\"]*)\"")
                .Select(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(classes => classes.Contains("bg-gold") && classes.Contains("text-white"))
                .Select(_ => Path.GetFileName(file)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ButtonClass_IsGoldDarkAtRest_AndGoldDarkerOnHover()
    {
        var rule = PrimaryButtonRule();

        Assert.Contains("bg-gold-dark", rule);
        Assert.Contains("hover:bg-gold-darker", rule);
        Assert.Contains("text-white", rule);
        // Disabled turns grey (not a faded gold), so it still reads as unavailable.
        Assert.Contains("disabled:bg-ink/15", rule);
        Assert.Contains("disabled:cursor-not-allowed", rule);
        Assert.DoesNotContain("disabled:opacity", rule);
    }

    [Theory]
    [InlineData("gold-dark")]
    [InlineData("gold-darker")]
    public void WhiteLabel_MeetsWcagAa_OnTheButtonColour(string themeColour)
    {
        var contrast = ContrastRatio("#ffffff", ThemeColour(themeColour));

        Assert.True(contrast >= WcagAaNormalText, $"White on {themeColour} is {contrast:F2}:1, below {WcagAaNormalText}:1");
    }

    [Fact]
    public void WhiteLabel_OnPlainGold_FailsWcagAa()
    {
        // Guards the contrast maths: plain gold is the colour the issue reported as too light.
        Assert.True(ContrastRatio("#ffffff", ThemeColour("gold")) < WcagAaNormalText);
    }

    private static string PagePath(string page) =>
        Path.Combine([CheckoutPaymentFormTests.WebProjectDirectory(), "Pages", .. page.Split('/')]);

    private static readonly string InputCss =
        File.ReadAllText(CheckoutPaymentFormTests.WebRootFile("assets", "css", "input.css"));

    private static string PrimaryButtonRule()
    {
        var match = Regex.Match(InputCss, @"\.btn-primary\s*\{\s*@apply\s+([^;]+);");
        Assert.True(match.Success, "No .btn-primary rule in input.css");
        return match.Groups[1].Value;
    }

    private static string ThemeColour(string name)
    {
        var match = Regex.Match(InputCss, $@"--color-{Regex.Escape(name)}:\s*(#[0-9a-fA-F]{{6}});");
        Assert.True(match.Success, $"No --color-{name} in input.css");
        return match.Groups[1].Value;
    }

    private static double ContrastRatio(string a, string b)
    {
        var (la, lb) = (RelativeLuminance(a), RelativeLuminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // WCAG 2.x relative luminance of an sRGB hex colour.
    private static double RelativeLuminance(string hex)
    {
        double Channel(int offset)
        {
            var c = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }
}
