using System.Data.Common;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CartIndexModel = GlimpsesOfGlory.Web.Pages.Cart.IndexModel;

namespace GlimpsesOfGlory.IntegrationTests;

// When checkout can't get stock, the shopper lands back on the cart with each short line flagged.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CheckoutUnavailableItemsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly ShippingAddressInfo PickupAddress = new("shopper@example.com", "Sam Shopper", null, null, null, null, null, null);

    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

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
    public async Task PlaceCashOrder_WhenAnotherOrderTakesTheStockBeforeReserving_RollsBackAndListsTheShortLine()
    {
        await SeedProductsAsync();
        // Prices fine (sunrise has 2), but another order takes one sunrise just as the transaction starts.
        var db = database.CreateDb(new CompetingOrderInterceptor("""UPDATE "Products" SET "StockQuantity" = 1 WHERE "Slug" = 'sunrise'"""));
        var cart = Cart(("harbour", 1), ("sunrise", 2));

        var result = await OrderService(db, cart).PlaceCashOrderAsync(PickupAddress, CancellationToken.None);

        Assert.Null(result.OrderId);
        Assert.Equal([new UnavailableCartLine("sunrise", 1)], result.UnavailableLines);
        await using var check = database.CreateDb();
        Assert.Empty(check.Orders);
        // harbour was reserved before sunrise failed; the rollback must put it back.
        Assert.Equal(5, check.Products.Single(p => p.Slug == "harbour").StockQuantity);
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
    public async Task CartPage_FlagsTheShortLines_WithoutTheGenericBanner()
    {
        var page = CartPage(Cart(("sunrise", 3), ("harbour", 1)), [new UnavailableCartLine("sunrise", 2)]);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.Banner);
        Assert.Equal([new CartLineError("sunrise", "Only 2 left. Lower the quantity to continue.")], page.LineErrors);
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

    private OrderService OrderService(CartSummary cart) => OrderService(database.CreateDb(), cart);

    private static OrderService OrderService(AppDbContext db, CartSummary cart) =>
        new(
            db,
            new StubCartService(cart),
            new FakePaymentGateway(),
            new OrderLineInventory(new EfInventoryStore(db)),
            new NullEmailSender(),
            TimeProvider.System,
            NullLogger<OrderService>.Instance);

    // Runs a write just before checkout's transaction begins, as a competing order committing
    // between pricing the cart and reserving its stock.
    private sealed class CompetingOrderInterceptor(string sql) : DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }

    private static CartSummary Cart(params (string Slug, int Quantity)[] lines) =>
        new(lines.Select(l => new CartLineView(l.Slug, l.Slug, null, 10m, l.Quantity, false, null)).ToList(), lines.Sum(l => 10m * l.Quantity), 8m);

    private static (PaymentModel Page, ITempDataDictionary TempData) PaymentPage(CheckoutPaymentResult result)
    {
        var (httpContext, sessionStore, tempData) = PickupCheckout();
        var page = new PaymentModel(
            new StubCartService(Cart(("sunrise", 3))),
            sessionStore,
            new StubOrderService(result),
            Options.Create(new StripeOptions { PublishableKey = "pk_test" }))
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = tempData,
        };
        return (page, tempData);
    }

    // A session with a pickup address already entered, as the checkout pages expect.
    private static (HttpContext HttpContext, CheckoutSessionStore SessionStore, ITempDataDictionary TempData) PickupCheckout()
    {
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(new CheckoutAddress { FulfillmentMethod = FulfillmentMethod.Pickup, Email = "shopper@example.com", FullName = "Sam Shopper" });
        return (httpContext, sessionStore, TempData(httpContext));
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

    private sealed class StubOrderService(CheckoutPaymentResult payment) : IOrderService
    {
        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) =>
            Task.FromResult(payment);

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
