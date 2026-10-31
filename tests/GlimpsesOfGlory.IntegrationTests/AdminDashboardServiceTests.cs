using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Options;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.IntegrationTests;

// Runs the dashboard metric queries against a migrated Postgres database, and checks each number
// agrees with the filtered admin list its tile links to.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AdminDashboardServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

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
        var orders = new AdminOrderService(database.CreateDb(), new OrderLineInventory(new FakeInventoryStore(new Dictionary<int, int>())), new FakePaymentGateway(), new NullEmailSender(), TimeProvider.System, NullLogger<AdminOrderService>.Instance);

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
    public async Task GetMetrics_CountsUnremindedAbandonedCheckouts_InTheListWindow_ExcludingConvertedAndReminded()
    {
        await AddPendingCheckoutAsync("counted@example.com", Now.AddHours(-2));
        await AddPendingCheckoutAsync("counted-old@example.com", Now.AddDays(-29));
        await AddPendingCheckoutAsync("too-new@example.com", Now.AddMinutes(-59));
        await AddPendingCheckoutAsync("too-old@example.com", Now.AddDays(-31));
        await AddPendingCheckoutAsync("reminded@example.com", Now.AddDays(-2), remindedAt: Now.AddDays(-1));
        await AddPendingCheckoutAsync("converted@example.com", Now.AddDays(-2));
        await AddOrderAsync(OrderStatus.New, PaymentMethod.Card, paidAt: Now.AddDays(-1), email: "converted@example.com", createdAt: Now.AddDays(-1));

        var metrics = await Service().GetMetricsAsync(CancellationToken.None);
        var listed = await new AbandonedCheckoutService(database.CreateDb(), new NullEmailSender(), new FixedTimeProvider(Now), NullLogger<AbandonedCheckoutService>.Instance)
            .GetAbandonedCheckoutsAsync(CancellationToken.None);

        Assert.Equal(2, metrics.UnremindedAbandonedCheckouts);
        Assert.Equal(metrics.UnremindedAbandonedCheckouts, listed.Count(c => c.RemindedAt is null));
    }

    private AdminDashboardService Service() => new(database.CreateDb(), new FixedTimeProvider(Now));

    private async Task AddOrderAsync(OrderStatus status, PaymentMethod paymentMethod, DateTimeOffset? paidAt = null, decimal subtotal = 20m, decimal shipping = 0m, string email = "shopper@example.com", DateTimeOffset? createdAt = null)
    {
        await using var db = database.CreateDb();
        db.Orders.Add(new Order
        {
            Email = email,
            FulfillmentMethod = paymentMethod == PaymentMethod.Cash ? FulfillmentMethod.Pickup : FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Status = status,
            PaymentMethod = paymentMethod,
            PaidAt = paidAt,
            Subtotal = subtotal,
            ShippingCost = shipping,
            CreatedAt = createdAt ?? Now.AddDays(-40),
        });
        await db.SaveChangesAsync();
    }

    private async Task AddPendingCheckoutAsync(string email, DateTimeOffset createdAt, DateTimeOffset? remindedAt = null)
    {
        await using var db = database.CreateDb();
        db.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = $"pi_{Guid.NewGuid():N}",
            Email = email,
            FulfillmentMethod = FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Subtotal = 20m,
            CreatedAt = createdAt,
            RemindedAt = remindedAt,
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
}
