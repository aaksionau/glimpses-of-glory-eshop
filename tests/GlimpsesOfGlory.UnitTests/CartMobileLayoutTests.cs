using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.UnitTests;

// Cart on mobile (#100): a fixed two-row line layout whatever the name's length, an estimated
// total, a full-width checkout CTA, a quiet Remove, product links, and a designed empty state.
public class CartMobileLayoutTests
{
    private const string CartLinesPath = "~/Pages/Cart/_CartLines.cshtml";

    private static readonly CartLineView Short = new("sunrise", "Sunrise", "sunrise.jpg", 20m, 2, false, null);
    private static readonly CartLineView Long = new("harbour", "Harbour at dusk with the fishing boats coming home under a long autumn sky", null, 15m, 1, false, null);

    [Fact]
    public async Task EveryLine_UsesTheSameFixedGrid_RegardlessOfNameLength()
    {
        var html = await RenderAsync(Cart(Short, Long));

        var lines = LineTags(html);
        Assert.Equal(2, lines.Count);
        var classes = lines.Select(l => Regex.Match(l, "class=\"([^\"]*)\"").Groups[1].Value).ToList();
        Assert.All(classes, c =>
        {
            Assert.Contains("grid grid-cols-[4rem_minmax(0,1fr)_auto]", c);
            Assert.Contains("sm:grid-cols-[4rem_minmax(0,1fr)_auto_6rem_auto]", c);
            Assert.DoesNotContain("flex-wrap", c);
        });
        Assert.Equal(classes[0], classes[1]);
    }

    [Fact]
    public async Task Line_OnMobile_PutsRemoveInTheFirstRow_AndStepperAndTotalInTheSecond()
    {
        var line = Line(await RenderAsync(Cart(Short)), "sunrise");

        Assert.Matches("<form class=\"col-start-3 row-start-1[^\"]*sm:col-start-5\"[^>]*handler=Remove", line);
        Assert.Matches("<form class=\"col-span-2 col-start-1 row-start-2[^\"]*sm:row-start-1\"[^>]*handler=UpdateQuantity", line);
        Assert.Matches("<p class=\"col-start-3 row-start-2[^\"]*sm:row-start-1\" data-testid=\"line-total\">\\$40\\.00</p>", line);
    }

    [Fact]
    public async Task Line_LinksTheThumbnailAndName_ToTheProductPage()
    {
        var line = Line(await RenderAsync(Cart(Short)), "sunrise");

        Assert.Matches("<a href=\"/products/sunrise\" tabindex=\"-1\" aria-hidden=\"true\"[^>]*>\\s*<img src=\"/product-photos/sunrise.jpg\" alt=\"\"", line);
        Assert.Matches("<a href=\"/products/sunrise\" class=\"[^\"]*\">Sunrise</a>", line);
    }

    [Fact]
    public async Task Remove_IsQuiet_NotRed()
    {
        var line = Line(await RenderAsync(Cart(Short)), "sunrise");
        var remove = Regex.Match(line, "<button type=\"submit\"[^>]*>Remove</button>").Value;

        Assert.Contains("text-ink-light", remove);
        Assert.DoesNotContain("text-red", remove);
    }

    [Fact]
    public async Task Totals_ShowSubtotalShippingAndEstimatedTotal()
    {
        var html = WebUtility.HtmlDecode(await RenderAsync(Cart(Short, Long)));

        Assert.Matches("Subtotal <span>\\$55\\.00</span>", html);
        Assert.Matches("data-testid=\"cart-shipping\">Shipping <span>\\$8\\.00 · or free local pickup</span>", html);
        Assert.Matches("data-testid=\"cart-estimated-total\">Estimated total <span>\\$63\\.00</span>", html);
    }

    [Fact]
    public async Task Totals_FromSm_SitInAFixedWidthColumn_SoValuesAlign()
    {
        var html = await RenderAsync(Cart(Short));

        // items-end would shrink each row to its content and break the justify-between columns.
        var totals = Regex.Match(html, "<div class=\"([^\"]*)\" data-testid=\"cart-totals\"").Groups[1].Value;
        Assert.Contains("sm:w-96", totals);
        Assert.DoesNotContain("items-end", totals);
    }

    [Fact]
    public async Task CheckoutCta_IsFullWidthOnMobile()
    {
        var html = await RenderAsync(Cart(Short));

        Assert.Matches("<a href=\"/checkout\" class=\"btn-primary [^\"]*\\bw-full sm:w-auto\">Proceed to checkout</a>", html);
    }

    [Fact]
    public async Task EmptyCart_HasADesignedStateWithABrowseCta()
    {
        var html = await RenderAsync(Cart());

        var empty = Regex.Match(html, "<div[^>]*data-testid=\"cart-empty\"[^>]*>.*?</div>", RegexOptions.Singleline).Value;
        Assert.Contains("Your cart is empty.", empty);
        Assert.Matches("<a href=\"/products\" class=\"btn-primary[^\"]*\">Browse products</a>", empty);
        Assert.DoesNotContain("/checkout", html);
    }

    private static List<string> LineTags(string html) =>
        Regex.Matches(html, "<div [^>]*data-cart-line=\"[^\"]+\"[^>]*>").Select(m => m.Value).ToList();

    // A line's markup: from its opening tag up to the next line (or the totals).
    private static string Line(string html, string slug)
    {
        var marker = html.IndexOf($"data-cart-line=\"{slug}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var start = html.LastIndexOf("<div", marker, StringComparison.Ordinal);
        var end = html.IndexOf("data-cart-line=", marker + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            end = html.IndexOf("data-testid=\"cart-totals\"", marker, StringComparison.Ordinal);
        }

        return html[start..end];
    }

    private static CartSummary Cart(params CartLineView[] lines) =>
        new(lines, lines.Sum(l => l.UnitPrice * l.Quantity), 8m);

    private static Task<string> RenderAsync(CartSummary cart) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(CartLinesPath, new CartLinesView(cart, []));
}
