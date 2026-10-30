using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Options;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using GlimpsesOfGlory.Web.Pages.Admin;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.UnitTests;

// Runs the dashboard metric queries against an in-memory SQLite database, and checks each number
// agrees with the filtered admin list its tile links to.
public sealed class AdminDashboardServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task GetMetrics_WithNothingInTheShop_IsAllZero()
    {
        Assert.Equal(new AdminDashboardMetrics(0, 0, 0, 0m, 0m), await Service().GetMetricsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetMetrics_CountsNewOrdersToFulfill()
    {
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Card, paidAt: Now);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.Shipped, PaymentMethod.Card, paidAt: Now);
        await AddOrderAsync(OrderStatus.PickedUp, PaymentMethod.Cash, paidAt: Now);
        await AddOrderAsync(OrderStatus.Cancelled, PaymentMethod.Cash);

        Assert.Equal(2, (await Service().GetMetricsAsync(CancellationToken.None)).OrdersToFulfill);
    }

    [Fact]
    public async Task GetMetrics_CountsUnpaidCashOrders_ThatArentCancelled()
    {
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.Shipped, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash, paidAt: Now);
        await AddOrderAsync(OrderStatus.Cancelled, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Card);

        Assert.Equal(2, (await Service().GetMetricsAsync(CancellationToken.None)).UnpaidCashOrders);
    }

    [Fact]
    public async Task GetMetrics_CountsVisibleLowStockProducts_ExcludingPreorders()
    {
        await AddProductAsync("out", stock: 0);
        await AddProductAsync("at-threshold", stock: 2);
        await AddProductAsync("plenty", stock: 3);
        await AddProductAsync("preorder", stock: 0, isPreorder: true);
        await AddProductAsync("hidden", stock: 1, isActive: false);

        Assert.Equal(2, (await Service().GetMetricsAsync(CancellationToken.None)).LowStockProducts);
    }

    [Fact]
    public async Task GetMetrics_RevenueSumsPaidNonCancelledOrders_ByWhenTheyWerePaid()
    {
        await AddOrderAsync(OrderStatus.Shipped, PaymentMethod.Card, paidAt: Now.AddDays(-1), subtotal: 40m, shipping: 5m);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash, paidAt: Now.AddDays(-6), subtotal: 10m);
        await AddOrderAsync(OrderStatus.PickedUp, PaymentMethod.Cash, paidAt: Now.AddDays(-10), subtotal: 100m);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Card, paidAt: Now.AddDays(-31), subtotal: 1000m);
        // Excluded: cancelled (even though paid), and unpaid.
        await AddOrderAsync(OrderStatus.Cancelled, PaymentMethod.Card, paidAt: Now.AddDays(-2), subtotal: 500m);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash, subtotal: 700m);

        var metrics = await Service().GetMetricsAsync(CancellationToken.None);

        Assert.Equal(55m, metrics.RevenueLast7Days);
        Assert.Equal(155m, metrics.RevenueLast30Days);
    }

    [Fact]
    public async Task OrderMetrics_MatchTheFilteredOrdersListsTheTilesLinkTo()
    {
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Card, paidAt: Now);
        await AddOrderAsync(OrderStatus.Shipped, PaymentMethod.Cash);
        await AddOrderAsync(OrderStatus.Cancelled, PaymentMethod.Cash);
        var metrics = await Service().GetMetricsAsync(CancellationToken.None);
        var orders = new AdminOrderService(database.CreateDb(), new FakeInventoryStore(new Dictionary<int, int>()), new FakePaymentGateway(), new NullEmailSender(), TimeProvider.System, NullLogger<AdminOrderService>.Instance);

        var toFulfill = await orders.GetOrdersAsync(AdminOrderTab.NeedsAction, new AdminOrderFilter(ToFulfillOnly: true), 1, 100, CancellationToken.None);
        var unpaidCash = await orders.GetOrdersAsync(AdminOrderTab.NeedsAction, new AdminOrderFilter(UnpaidCashOnly: true), 1, 100, CancellationToken.None);

        Assert.Equal(2, metrics.OrdersToFulfill);
        Assert.Equal(metrics.OrdersToFulfill, toFulfill.TotalCount);
        Assert.Equal(2, metrics.UnpaidCashOrders);
        Assert.Equal(metrics.UnpaidCashOrders, unpaidCash.TotalCount);
    }

    [Fact]
    public async Task LowStockMetric_MatchesTheFilteredProductsList()
    {
        await AddProductAsync("low", stock: 1);
        await AddProductAsync("plenty", stock: 10);
        await AddProductAsync("preorder", stock: 0, isPreorder: true);
        var metrics = await Service().GetMetricsAsync(CancellationToken.None);
        var products = new AdminProductService(database.CreateDb(), new ProductPhotoStorage(new ProductPhotoStorageOptions { StoragePath = Path.GetTempPath() }));

        var lowStock = await products.GetProductsAsync(lowStockOnly: true, CancellationToken.None);
        var all = await products.GetProductsAsync(lowStockOnly: false, CancellationToken.None);

        Assert.Equal(["low"], lowStock.Select(p => p.Name));
        Assert.Equal(metrics.LowStockProducts, lowStock.Count);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task DashboardPage_LinksEachCountTileToItsFilteredList()
    {
        var model = new IndexModel(new StubDashboardService(new AdminDashboardMetrics(3, 1, 4, 55m, 155m)));

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            [
                ("Orders to fulfill", "3", "/admin/orders?tofulfill=true"),
                ("Unpaid cash orders", "1", "/admin/orders?unpaidcash=true"),
                ("Low-stock products", "4", "/admin/products?lowStock=true"),
            ],
            model.Tiles.Take(3).Select(t => (t.Label, t.Value, t.Href)));
        Assert.Equal(55m.ToString("C"), model.Tiles[3].Value);
        Assert.Equal(155m.ToString("C"), model.Tiles[4].Value);
        // There's no revenue list to link to.
        Assert.All(model.Tiles.Skip(3), t => Assert.Null(t.Href));
    }

    private AdminDashboardService Service() => new(database.CreateDb(), new FixedTimeProvider(Now));

    private async Task AddOrderAsync(OrderStatus status, PaymentMethod paymentMethod, DateTimeOffset? paidAt = null, decimal subtotal = 20m, decimal shipping = 0m)
    {
        await using var db = database.CreateDb();
        db.Orders.Add(new Order
        {
            Email = "shopper@example.com",
            FulfillmentMethod = paymentMethod == PaymentMethod.Cash ? FulfillmentMethod.Pickup : FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Status = status,
            PaymentMethod = paymentMethod,
            PaidAt = paidAt,
            Subtotal = subtotal,
            ShippingCost = shipping,
            CreatedAt = Now.AddDays(-40),
        });
        await db.SaveChangesAsync();
    }

    private async Task AddProductAsync(string name, int stock, bool isPreorder = false, bool isActive = true)
    {
        await using var db = database.CreateDb();
        db.Products.Add(new Product
        {
            Name = name,
            Slug = name,
            Description = name,
            Price = 10m,
            StockQuantity = stock,
            IsPreorder = isPreorder,
            IsActive = isActive,
        });
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubDashboardService(AdminDashboardMetrics metrics) : IAdminDashboardService
    {
        public Task<AdminDashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken) => Task.FromResult(metrics);
    }
}
