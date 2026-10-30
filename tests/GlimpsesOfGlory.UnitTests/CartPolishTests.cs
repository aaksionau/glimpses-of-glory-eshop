using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using CartIndexModel = GlimpsesOfGlory.Web.Pages.Cart.IndexModel;

namespace GlimpsesOfGlory.UnitTests;

// Cart and checkout polish (#85): undo after remove, the quantity stepper, the highlighted
// delivery option, and the trust cues beside the Pay button.
public class CartPolishTests
{
    private const string CartLinesPath = "~/Pages/Cart/_CartLines.cshtml";

    private static readonly CartLineView Sunrise = new("sunrise", "Sunrise over the lake", null, 20m, 2, false, null);
    private static readonly CartLineView Harbour = new("harbour", "Harbour at dusk", null, 15m, 1, false, null);

    [Fact]
    public async Task Remove_OffersTheRemovedLineBack_WithItsQuantity()
    {
        var cartService = new InMemoryCartService(Sunrise, Harbour);

        var result = await CartPage(cartService).OnPostRemoveAsync("sunrise", CancellationToken.None);

        var view = Assert.IsType<CartLinesView>(Assert.IsType<PartialViewResult>(result).Model);
        Assert.Equal(Sunrise, view.RemovedLine);
        Assert.Equal(["harbour"], view.Cart.Lines.Select(l => l.ProductSlug));
    }

    [Fact]
    public async Task UndoRemove_PutsTheLineBack()
    {
        var cartService = new InMemoryCartService(Harbour);

        var result = await CartPage(cartService).OnPostUndoRemoveAsync("sunrise", 2, CancellationToken.None);

        var view = Assert.IsType<CartLinesView>(Assert.IsType<PartialViewResult>(result).Model);
        Assert.Null(view.RemovedLine);
        Assert.Null(view.Message);
        Assert.Equal([("sunrise", 2)], cartService.Added);
    }

    [Fact]
    public async Task UndoRemove_ThatCanNoLongerAddTheLine_ShowsWhy()
    {
        var cartService = new InMemoryCartService(Harbour) { AddResult = CartOperationResult.Failed("Sunrise over the lake is out of stock.") };

        var result = await CartPage(cartService).OnPostUndoRemoveAsync("sunrise", 2, CancellationToken.None);

        var view = Assert.IsType<CartLinesView>(Assert.IsType<PartialViewResult>(result).Model);
        Assert.Equal("Sunrise over the lake is out of stock.", view.Message);
    }

    [Fact]
    public async Task CartLines_AfterRemoving_ShowRemovedAndAnUndoThatPostsTheLineBack()
    {
        var html = WebUtility.HtmlDecode(await RenderCartLinesAsync(new CartLinesView(Cart(Harbour), [], RemovedLine: Sunrise)));

        var notice = Regex.Match(html, "<div[^>]*data-removed-line=\"sunrise\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(notice.Success);
        Assert.Contains("role=\"status\"", notice.Value);
        Assert.Contains("Removed Sunrise over the lake", notice.Value);
        Assert.Contains("hx-post=\"/cart?handler=UndoRemove\"", notice.Value);
        Assert.Contains("name=\"slug\" value=\"sunrise\"", notice.Value);
        Assert.Contains("name=\"quantity\" value=\"2\"", notice.Value);
        Assert.Contains(">Undo</button>", notice.Value);
    }

    [Fact]
    public async Task CartLines_AfterRemovingTheLastLine_StillOfferUndo()
    {
        var html = await RenderCartLinesAsync(new CartLinesView(Cart(), [], RemovedLine: Sunrise));

        Assert.Contains("Your cart is empty.", html);
        Assert.Contains("data-removed-line=\"sunrise\"", html);
    }

    [Fact]
    public async Task CartLines_WithoutARemoval_ShowNoUndo()
    {
        var html = await RenderCartLinesAsync(new CartLinesView(Cart(Sunrise), []));

        Assert.DoesNotContain("data-removed-line", html);
        Assert.DoesNotContain("UndoRemove", html);
    }

    [Fact]
    public async Task CartLines_ShowTheMessage_AsAnAlert()
    {
        var html = await RenderCartLinesAsync(new CartLinesView(Cart(Harbour), [], Message: "Sunrise is out of stock."));

        Assert.Contains("<p class=\"text-red-600 mb-4\" role=\"alert\">Sunrise is out of stock.</p>", html);
    }

    [Fact]
    public async Task CartLines_QuantityHasMinusAndPlusButtons_AndASavingIndicator()
    {
        var html = await RenderCartLinesAsync(new CartLinesView(Cart(Sunrise), []));
        var form = Regex.Match(html, "<form[^>]*handler=UpdateQuantity.*?</form>", RegexOptions.Singleline).Value;

        Assert.Matches("<button type=\"button\" id=\"qty-down-sunrise\" aria-label=\"Decrease quantity of Sunrise over the lake\"[^>]*x-on:click=\"step\\(false\\)\"", form);
        Assert.Matches("<button type=\"button\" id=\"qty-up-sunrise\" aria-label=\"Increase quantity of Sunrise over the lake\"[^>]*x-on:click=\"step\\(true\\)\"", form);
        Assert.Contains("x-ref=\"qty\"", form);
        Assert.Contains("hx-indicator=\"#saving-sunrise\"", form);
        Assert.Matches("<svg id=\"saving-sunrise\" class=\"htmx-indicator", form);
        // Buttons, input, buttons in reading order.
        Assert.True(form.IndexOf("qty-down-sunrise", StringComparison.Ordinal) < form.IndexOf("id=\"qty-sunrise\"", StringComparison.Ordinal));
        Assert.True(form.IndexOf("id=\"qty-sunrise\"", StringComparison.Ordinal) < form.IndexOf("qty-up-sunrise", StringComparison.Ordinal));
    }

    [Fact]
    public void CheckoutDeliveryOptions_HighlightTheCheckedCard()
    {
        var page = File.ReadAllText(Path.Combine(CheckoutPaymentFormTests.WebProjectDirectory(), "Pages", "Checkout", "Index.cshtml"));
        var labels = Regex.Matches(page, "<label class=\"([^\"]*)\">\\s*<input asp-for=\"Address.FulfillmentMethod\"");

        Assert.Equal(2, labels.Count);
        Assert.All(labels, l => Assert.Contains("has-checked:border-gold has-checked:bg-gold-light/30", l.Groups[1].Value));
    }

    [Fact]
    public async Task PaymentForm_ShowsStripeAndTheAcceptedCards_BelowThePayButton()
    {
        var page = await CheckoutOrderSummaryTests.CreatePaymentPageAsync(
            new CheckoutPaymentSetup("pi_1", "pi_1_secret_abc", Cart(Sunrise)), publishableKey: "pk_test_123");
        var html = WebUtility.HtmlDecode(await RazorViewToStringRendererTests.CreateRenderer().RenderAsync("~/Pages/Checkout/_PaymentForm.cshtml", page));

        var trust = html.IndexOf("data-testid=\"payment-trust\"", StringComparison.Ordinal);
        Assert.True(trust > html.IndexOf("id=\"submit-payment\"", StringComparison.Ordinal));
        Assert.Contains("Secure payment by Stripe", html[trust..]);
        Assert.Contains("aria-label=\"Accepted cards\"", html[trust..]);
        Assert.All(PaymentLabels.AcceptedCards, card => Assert.Contains($">{card}</li>", html[trust..]));
    }

    private static CartSummary Cart(params CartLineView[] lines) =>
        new(lines, lines.Sum(l => l.UnitPrice * l.Quantity), 8m);

    private static Task<string> RenderCartLinesAsync(CartLinesView view) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(CartLinesPath, view);

    private static CartIndexModel CartPage(ICartService cartService) =>
        new(cartService)
        {
            // PageModel.Partial reads ViewData and metadata the framework normally supplies.
            MetadataProvider = new EmptyModelMetadataProvider(),
            PageContext = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new CompiledPageActionDescriptor()))
            {
                ViewData = new ViewDataDictionary<CartIndexModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            },
        };

    // Holds lines in memory; adds are recorded (and re-add the line as given) rather than priced.
    private sealed class InMemoryCartService(params CartLineView[] lines) : ICartService
    {
        private readonly List<CartLineView> lines = [.. lines];

        public List<(string Slug, int Quantity)> Added { get; } = [];

        public CartOperationResult AddResult { get; init; } = CartOperationResult.Ok();

        public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(Cart([.. lines]));

        public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken)
        {
            if (AddResult.Success)
            {
                Added.Add((slug, quantity));
                lines.Add(new CartLineView(slug, slug, null, 10m, quantity, false, null));
            }

            return Task.FromResult(AddResult);
        }

        public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLineAsync(string slug, CancellationToken cancellationToken)
        {
            lines.RemoveAll(l => l.ProductSlug == slug);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
