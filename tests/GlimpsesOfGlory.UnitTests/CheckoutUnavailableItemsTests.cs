using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CartIndexModel = GlimpsesOfGlory.Web.Pages.Cart.IndexModel;

namespace GlimpsesOfGlory.UnitTests;

// When checkout can't get stock, the shopper lands back on the cart with each short line flagged.
public sealed class CheckoutUnavailableItemsTests : IDisposable
{
    private static readonly ShippingAddressInfo PickupAddress = new("shopper@example.com", "Sam Shopper", null, null, null, null, null, null);

    private readonly SqliteTestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task CreatePaymentIntent_ListsEachShortLine_WithHowManyAreLeft()
    {
        await SeedProductsAsync();
        var cart = Cart(("sunrise", 3), ("harbour", 1), ("gone", 2), ("meadow", 1));

        var result = await OrderService(cart).CreatePaymentIntentAsync(FulfillmentMethod.Shipping, PickupAddress, null, CancellationToken.None);

        Assert.Null(result.Setup);
        Assert.Equal(
            [new UnavailableCartLine("sunrise", 2), new UnavailableCartLine("gone", 0), new UnavailableCartLine("meadow", 0)],
            result.UnavailableLines);
    }

    [Fact]
    public async Task PlaceCashOrder_ListsEachShortLine_AndPlacesNoOrder()
    {
        await SeedProductsAsync();
        var cart = Cart(("sunrise", 5), ("harbour", 1));

        var result = await OrderService(cart).PlaceCashOrderAsync(PickupAddress, CancellationToken.None);

        Assert.Null(result.OrderId);
        Assert.Equal([new UnavailableCartLine("sunrise", 2)], result.UnavailableLines);
        await using var db = database.CreateDb();
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task PlaceCashOrder_WithAnEmptyCart_FailsWithoutLineDetail()
    {
        var result = await OrderService(Cart()).PlaceCashOrderAsync(PickupAddress, CancellationToken.None);

        Assert.Null(result.OrderId);
        Assert.Empty(result.UnavailableLines);
    }

    [Fact]
    public async Task PlaceCashOrder_WhenStockIsThere_PlacesTheOrder()
    {
        await SeedProductsAsync();

        var result = await OrderService(Cart(("sunrise", 2), ("harbour", 1))).PlaceCashOrderAsync(PickupAddress, CancellationToken.None);

        Assert.NotNull(result.OrderId);
        Assert.Empty(result.UnavailableLines);
    }

    [Fact]
    public async Task PaymentPage_SendsEachShortLineToTheCart_InsteadOfTheGenericBanner()
    {
        var (page, tempData) = PaymentPage(CheckoutPaymentResult.Failed([new UnavailableCartLine("sunrise", 2), new UnavailableCartLine("gone", 0)]));

        var result = await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("/Cart/Index", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Null(tempData["Error"]);
        Assert.Equal(
            [
                new CartLineError("sunrise", "Only 2 left. Lower the quantity to continue."),
                new CartLineError("gone", "No longer available. Remove it to continue."),
            ],
            CartLineErrorsTempData.Read(tempData));
    }

    [Fact]
    public async Task PaymentPage_WithoutLineDetail_FallsBackToTheGenericBanner()
    {
        var (page, tempData) = PaymentPage(CheckoutPaymentResult.Failed([]));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(CheckoutMessages.ItemsUnavailable, tempData["Error"]);
        Assert.Empty(CartLineErrorsTempData.Read(tempData));
    }

    [Fact]
    public async Task ReviewPage_CashOrderThatRanShort_SendsEachShortLineToTheCart()
    {
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(new CheckoutAddress { FulfillmentMethod = FulfillmentMethod.Pickup, Email = "shopper@example.com", FullName = "Sam Shopper" });
        var tempData = TempData(httpContext);
        var page = new ReviewModel(
            new StubCartService(Cart(("sunrise", 3))),
            new StubOrderService(cashOrder: CashOrderResult.Failed([new UnavailableCartLine("sunrise", 2)])),
            sessionStore)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = tempData,
        };

        var result = await page.OnPostPickupPaymentAsync(PaymentMethod.Cash, CancellationToken.None);

        Assert.Equal("/Cart/Index", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal([new CartLineError("sunrise", "Only 2 left. Lower the quantity to continue.")], CartLineErrorsTempData.Read(tempData));
    }

    [Fact]
    public async Task CartPage_FlagsTheShortLines_WithoutTheGenericBanner()
    {
        var page = CartPage(Cart(("sunrise", 3), ("harbour", 1)), [new UnavailableCartLine("sunrise", 2)]);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.Banner);
        Assert.Equal([new CartLineError("sunrise", "Only 2 left. Lower the quantity to continue.")], page.LineErrors);
    }

    [Fact]
    public async Task CartPage_ShowsTheGenericBanner_WhenNoneOfTheFlaggedLinesAreStillInTheCart()
    {
        var page = CartPage(Cart(("harbour", 1)), [new UnavailableCartLine("sunrise", 2)]);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(CheckoutMessages.ItemsUnavailable, page.Banner);
        Assert.Empty(page.LineErrors);
    }

    [Fact]
    public async Task CartLines_ShowEachLinesMessageUnderThatLine_AndMarkItsQuantityInvalid()
    {
        var cart = Cart(("sunrise", 3), ("harbour", 1));
        var view = new CartLinesView(cart, [new CartLineError("sunrise", "Only 2 left. Lower the quantity to continue.")]);

        var html = await RazorViewToStringRendererTests.CreateRenderer().RenderAsync("~/Pages/Cart/_CartLines.cshtml", view);

        Assert.Contains("<p class=\"w-full text-red-600 text-sm\" id=\"error-sunrise\">Only 2 left. Lower the quantity to continue.</p>", html);
        Assert.Contains("aria-describedby=\"error-sunrise\"", html);
        Assert.DoesNotContain("error-harbour", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "aria-invalid=\"true\""));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-line-error=\"true\""));
    }

    // sunrise: 2 in stock; harbour: 5 in stock; meadow: sold out; "gone" isn't in the catalogue.
    private async Task SeedProductsAsync()
    {
        await using var db = database.CreateDb();
        db.Products.AddRange(
            new Product { Name = "Sunrise", Slug = "sunrise", Description = "d", Price = 20m, StockQuantity = 2 },
            new Product { Name = "Harbour", Slug = "harbour", Description = "d", Price = 15m, StockQuantity = 5 },
            new Product { Name = "Meadow", Slug = "meadow", Description = "d", Price = 10m, StockQuantity = 0 });
        await db.SaveChangesAsync();
    }

    private OrderService OrderService(CartSummary cart)
    {
        var db = database.CreateDb();
        return new OrderService(
            db,
            new StubCartService(cart),
            new FakePaymentGateway(),
            new OrderLineInventory(new EfInventoryStore(db)),
            new NullEmailSender(),
            TimeProvider.System,
            NullLogger<OrderService>.Instance);
    }

    private static CartSummary Cart(params (string Slug, int Quantity)[] lines) =>
        new(lines.Select(l => new CartLineView(l.Slug, l.Slug, null, 10m, l.Quantity, false, null)).ToList(), lines.Sum(l => 10m * l.Quantity), 8m);

    private static (PaymentModel Page, ITempDataDictionary TempData) PaymentPage(CheckoutPaymentResult result)
    {
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(new CheckoutAddress { FulfillmentMethod = FulfillmentMethod.Pickup, Email = "shopper@example.com", FullName = "Sam Shopper" });
        var tempData = TempData(httpContext);
        var page = new PaymentModel(
            new StubCartService(Cart(("sunrise", 3))),
            sessionStore,
            new StubOrderService(payment: result),
            Options.Create(new StripeOptions { PublishableKey = "pk_test" }))
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = tempData,
        };
        return (page, tempData);
    }

    private static CartIndexModel CartPage(CartSummary cart, IReadOnlyList<UnavailableCartLine> unavailableLines)
    {
        var httpContext = new DefaultHttpContext();
        var tempData = TempData(httpContext);
        CartLineErrorsTempData.ReportUnavailable(tempData, unavailableLines);
        return new CartIndexModel(new StubCartService(cart))
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = tempData,
        };
    }

    private static TempDataDictionary TempData(HttpContext httpContext) => new(httpContext, new NullTempDataProvider());

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class StubCartService(CartSummary cart) : ICartService
    {
        public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(cart);

        public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLineAsync(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubOrderService(CheckoutPaymentResult? payment = null, CashOrderResult? cashOrder = null) : IOrderService
    {
        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) =>
            Task.FromResult(payment ?? throw new NotSupportedException());

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) =>
            Task.FromResult(cashOrder ?? throw new NotSupportedException());

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
