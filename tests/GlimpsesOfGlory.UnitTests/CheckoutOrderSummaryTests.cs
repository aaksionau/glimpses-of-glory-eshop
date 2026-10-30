using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutOrderSummaryTests
{
    private const string PartialPath = "~/Pages/Checkout/_CheckoutOrderSummary.cshtml";

    // Two items at $20 + one at $15.50 = $55.50 subtotal; shipping to an address costs $8.
    private static readonly CartSummary Cart = new(
        [
            new CartLineView("sunrise", "Sunrise over the lake", "sunrise.jpg", 20m, 2, false, null),
            new CartLineView("harbour", "Harbour at dusk", null, 15.50m, 1, false, null),
        ],
        Subtotal: 55.50m,
        ShippingCost: 8m);

    [Fact]
    public async Task Summary_ListsLinesSubtotalShippingAndTotal()
    {
        var html = await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Shipping));
        var text = Text(html);

        Assert.Contains("Sunrise over the lake", text);
        Assert.Contains("Qty 2 × $20.00", text);
        Assert.Contains("$40.00", text);
        Assert.Contains("Harbour at dusk", text);
        Assert.Contains("Subtotal: $55.50", text);
        Assert.Contains("Shipping: $8.00", text);
        Assert.Contains("Total: $63.50", text);
    }

    [Fact]
    public async Task Summary_ForPickup_ShowsFreePickupInTotal()
    {
        var text = Text(await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Pickup)));

        Assert.Contains("Pickup: $0.00", text);
        Assert.Contains("Total: $55.50", text);
        Assert.DoesNotContain("Shipping:", text);
    }

    [Fact]
    public async Task CollapsibleSummary_HasToggleShowingTheTotal_AndStartsClosedOnSmallScreens()
    {
        var html = await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Shipping));

        var toggle = Regex.Match(html, "<button\\b[^>]*>.*?</button>", RegexOptions.Singleline).Value;
        Assert.Contains("lg:hidden", toggle);
        Assert.Contains("aria-controls=\"order-summary-content\"", toggle);
        Assert.Contains("aria-expanded=\"false\"", toggle);
        Assert.Contains("x-on:click=\"summaryOpen = !summaryOpen\"", toggle);
        Assert.Matches("Show order summary.*·\\s*\\$63\\.50", Text(toggle));

        Assert.Contains("x-data=\"{ summaryOpen: false }\"", html);
        var content = Tag(html, "order-summary-content");
        Assert.Matches("class=\"hidden [^\"]*lg:block", content);
        Assert.Contains("{ &#x27;hidden&#x27;: !summaryOpen }", content);
    }

    [Fact]
    public async Task NonCollapsibleSummary_IsAlwaysOpen()
    {
        var html = await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Shipping, Collapsible: false));

        Assert.DoesNotContain("<button", html);
        Assert.DoesNotContain("summaryOpen }", Tag(html, "order-summary-content"));
    }

    [Fact]
    public async Task SummaryFollowingTheChoice_RendersEachMethodsTotal_ShowingOnlyTheSelectedOne()
    {
        var html = await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Shipping, FollowsChoice: true));

        var shippingTotal = MethodRows(html, "Total: \\$63\\.50");
        var pickupTotal = MethodRows(html, "Total: \\$55\\.50");
        Assert.Contains("x-show=\"method === &#x27;Shipping&#x27;\"", shippingTotal);
        Assert.DoesNotContain("display: none", shippingTotal);
        Assert.Contains("x-show=\"method === &#x27;Pickup&#x27;\"", pickupTotal);
        Assert.Contains("style=\"display: none;\"", pickupTotal);
    }

    [Fact]
    public async Task SummaryForAFixedMethod_HasNoAlpineMethodBinding()
    {
        var html = await RenderAsync(new CheckoutOrderSummary(Cart, FulfillmentMethod.Shipping));

        Assert.DoesNotContain("x-show", html);
        Assert.DoesNotContain("Total: $55.50", html);
    }

    [Fact]
    public async Task PaymentPage_SummarisesAndShowsTheAmountCharged_EvenWhenAPriceChangedSinceAddingToCart()
    {
        // The catalogue price of "sunrise" went from $20 to $25 after it was added to the cart:
        // 2 × $25 + $15.50 = $65.50 subtotal, + $8 shipping = $73.50 charged.
        var pricedCart = Cart with
        {
            Lines = [Cart.Lines[0] with { UnitPrice = 25m }, Cart.Lines[1]],
            Subtotal = 65.50m,
        };

        var page = await CreatePaymentPageAsync(new CheckoutPaymentSetup("pi_1", "secret_1", pricedCart));

        Assert.Equal(FulfillmentMethod.Shipping, page.Summary.FulfillmentMethod);
        Assert.Same(pricedCart, page.Summary.Cart);
        Assert.Equal(73.50m, page.Summary.Total);
        Assert.Equal(73.50m, page.AmountDue);
    }

    // The Payment page after OnGetAsync for a shipping order, with the PaymentIntent set up as given.
    internal static async Task<PaymentModel> CreatePaymentPageAsync(CheckoutPaymentSetup setup, string publishableKey = "pk_test")
    {
        var address = new CheckoutAddress
        {
            FulfillmentMethod = FulfillmentMethod.Shipping,
            Email = "shopper@example.com",
            FullName = "Sam Shopper",
            AddressLine1 = "1 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "United States",
        };
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(address);
        var page = new PaymentModel(
            new StubCartService(Cart),
            sessionStore,
            new StubOrderService(setup),
            Options.Create(new StripeOptions { PublishableKey = publishableKey }))
        {
            PageContext = new PageContext { HttpContext = httpContext },
        };

        await page.OnGetAsync(CancellationToken.None);
        return page;
    }

    private static Task<string> RenderAsync(CheckoutOrderSummary model) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(PartialPath, model);

    // The opening tag of the per-method shipping/total rows whose content matches the pattern.
    private static string MethodRows(string html, string pattern) =>
        Regex.Match(html, $"<div\\b[^>]*x-show[^>]*>(?=(?:(?!</div>).)*{pattern})", RegexOptions.Singleline).Value;

    private static string Tag(string html, string id) =>
        Regex.Match(html, $"<[a-z]+\\b[^>]*id=\"{id}\"[^>]*>").Value;

    // Visible text with tags stripped, entities decoded and whitespace collapsed.
    internal static string Text(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ");

    private sealed class StubOrderService(CheckoutPaymentSetup setup) : IOrderService
    {
        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) =>
            Task.FromResult(CheckoutPaymentResult.Ready(setup));

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
