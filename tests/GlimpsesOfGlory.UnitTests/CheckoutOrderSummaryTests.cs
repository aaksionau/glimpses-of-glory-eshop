using System.Diagnostics.CodeAnalysis;
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
        Assert.Contains("lg:block", content);
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

        var shippingTotal = Regex.Match(html, "<p\\b[^>]*>Total: \\$63\\.50</p>").Value;
        var pickupTotal = Regex.Match(html, "<p\\b[^>]*>Total: \\$55\\.50</p>").Value;
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
    public async Task PaymentPage_ChargesAndSummarisesTheOrderTotal()
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
            new StubOrderService(new PaymentIntentSetup("pi_1", "secret_1", 63.50m)),
            Options.Create(new StripeOptions { PublishableKey = "pk_test" }))
        {
            PageContext = new PageContext { HttpContext = httpContext },
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(63.50m, page.AmountDue);
        Assert.Equal(FulfillmentMethod.Shipping, page.Summary.FulfillmentMethod);
        Assert.Equal(63.50m, page.Summary.Total);
    }

    private static Task<string> RenderAsync(CheckoutOrderSummary model) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(PartialPath, model);

    private static string Tag(string html, string id) =>
        Regex.Match(html, $"<[a-z]+\\b[^>]*id=\"{id}\"[^>]*>").Value;

    // Visible text with tags stripped, entities decoded and whitespace collapsed.
    private static string Text(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ");

    private sealed class StubCartService(CartSummary cart) : ICartService
    {
        public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(cart);

        public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLineAsync(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubOrderService(PaymentIntentSetup setup) : IOrderService
    {
        public Task<PaymentIntentSetup?> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentIntentSetup?>(setup);

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int?> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _values.Remove(key);

        public void Set(string key, byte[] value) => _values[key] = value;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
