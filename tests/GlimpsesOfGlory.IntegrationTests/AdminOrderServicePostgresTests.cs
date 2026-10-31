using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.IntegrationTests;

// AdminOrderService against real Postgres: the paths whose correctness depends on how Postgres
// locks rows, runs transactions, translates string functions and stores timestamptz.
[Collection(PostgresCollection.Name)]
public sealed class AdminOrderServicePostgresTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task ConcurrentMarkShipped_OnlyAppliesOnce_AndEmailsTheBuyerOnce()
    {
        var id = await AddOrderAsync();
        var emailSender = new RecordingEmailSender();
        var rendezvous = new FirstQueryRendezvous(participants: 2);
        await using var first = database.CreateDb(rendezvous.CreateInterceptor());
        await using var second = database.CreateDb(rendezvous.CreateInterceptor());

        var results = await Task.WhenAll(
            Service(first, emailSender).MarkShippedAsync(id, "TRACK-A", ShippingCarrier.Usps, CancellationToken.None),
            Service(second, emailSender).MarkShippedAsync(id, "TRACK-B", ShippingCarrier.Other, CancellationToken.None));

        Assert.Single(results, OrderUpdateResult.Updated);
        Assert.Single(results, OrderUpdateResult.InvalidTransition);
        var order = await LoadAsync(id);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        var sent = Assert.Single(emailSender.Shipped);
        Assert.Equal(order.TrackingNumber, sent.TrackingNumber);
    }

    [Fact]
    public async Task ConcurrentMarkCancelled_RestocksExactlyOnce()
    {
        var productId = await AddProductAsync(stock: 3);
        var id = await AddOrderAsync(lines: [Line(productId, quantity: 2)]);
        var rendezvous = new FirstQueryRendezvous(participants: 2);
        await using var first = database.CreateDb(rendezvous.CreateInterceptor());
        await using var second = database.CreateDb(rendezvous.CreateInterceptor());

        var results = await Task.WhenAll(
            Service(first).MarkCancelledAsync(id, CancellationToken.None),
            Service(second).MarkCancelledAsync(id, CancellationToken.None));

        Assert.Single(results, OrderUpdateResult.Updated);
        Assert.Single(results, OrderUpdateResult.InvalidTransition);
        Assert.Equal(OrderStatus.Cancelled, (await LoadAsync(id)).Status);
        Assert.Equal(5, await StockAsync(productId));
    }

    [Fact]
    public async Task MarkCancelled_WhenARestockFails_LeavesTheOrderNew_AndRollsBackEarlierRestocks()
    {
        var firstProduct = await AddProductAsync(stock: 3);
        var secondProduct = await AddProductAsync(stock: 4);
        var id = await AddOrderAsync(lines: [Line(firstProduct, quantity: 2), Line(secondProduct, quantity: 1)]);
        await using var db = database.CreateDb();
        var inventory = new FailingRestockInventoryStore(new EfInventoryStore(db), failOnRestock: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(db, inventoryStore: inventory).MarkCancelledAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Null(order.CancelledAt);
        Assert.Equal(3, await StockAsync(firstProduct));
        Assert.Equal(4, await StockAsync(secondProduct));
    }

    [Fact]
    public async Task MarkCancelled_ThenAgain_RestocksOnlyTheFirstTime()
    {
        var productId = await AddProductAsync(stock: 3);
        var id = await AddOrderAsync(lines: [Line(productId, quantity: 2)]);

        await using (var db = database.CreateDb())
        {
            Assert.Equal(OrderUpdateResult.Updated, await Service(db).MarkCancelledAsync(id, CancellationToken.None));
        }
        await using (var db = database.CreateDb())
        {
            Assert.Equal(OrderUpdateResult.InvalidTransition, await Service(db).MarkCancelledAsync(id, CancellationToken.None));
        }

        Assert.Equal(5, await StockAsync(productId));
        Assert.Equal(Now, (await LoadAsync(id)).CancelledAt);
    }

    [Fact]
    public async Task Search_MatchesEmailAndName_IgnoringCase_AndOrderNumbers()
    {
        var alice = await AddOrderAsync(email: "Alice.Walker@Example.COM", customerName: "Alice Walker");
        var bob = await AddOrderAsync(email: "bob@shop.test", customerName: "ROBERT Stone");
        await AddOrderAsync(email: "carol@example.com", customerName: "Carol King");

        Assert.Equal([alice], await SearchAsync("walker@example"));
        Assert.Equal([alice], await SearchAsync("ICE WAL"));
        Assert.Equal([bob], await SearchAsync("Robert"));
        Assert.Equal([bob], await SearchAsync("SHOP.TE"));
        Assert.Equal([bob], await SearchAsync($"#{bob}"));
        Assert.Equal([bob], await SearchAsync($" # {bob} "));
        Assert.Empty(await SearchAsync("#999999"));
        Assert.Empty(await SearchAsync("nobody"));
        Assert.Equal(3, (await SearchAsync("   ")).Count);
    }

    [Fact]
    public async Task OrdersList_IsNewestFirst_ByCreatedAt_WithIdBreakingTies()
    {
        var oldest = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
        var newest = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 10, 30, 23, 59, 59, 999, TimeSpan.Zero));
        var tiedA = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var middle = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 6, 1, 12, 0, 0, 1, TimeSpan.Zero));
        var tiedB = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var justBeforeNewest = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 10, 30, 23, 59, 59, 998, TimeSpan.Zero));

        var expected = new[] { newest, justBeforeNewest, middle, tiedB, tiedA, oldest };

        await using var db = database.CreateDb();
        var service = Service(db);
        var onePage = (await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, 1, 50, CancellationToken.None)).Orders;
        Assert.Equal(expected, onePage.Select(o => o.Id));
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 23, 59, 59, 999, TimeSpan.Zero), onePage[0].CreatedAt);

        var paged = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            paged.AddRange((await service.GetOrdersAsync(AdminOrderTab.All, AdminOrderFilter.None, page, 2, CancellationToken.None)).Orders.Select(o => o.Id));
        }
        Assert.Equal(expected, paged);
    }

    [Fact]
    public async Task Export_ReturnsTheWholeUtcDayRange_OldestFirst()
    {
        await AddOrderAsync(createdAt: new DateTimeOffset(2026, 9, 30, 23, 59, 59, 999, TimeSpan.Zero));
        var lastMoment = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 10, 31, 23, 59, 59, 999, TimeSpan.Zero));
        var first = await AddOrderAsync(createdAt: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await AddOrderAsync(createdAt: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        await using var db = database.CreateDb();
        var rows = await Service(db).StreamOrdersForExportAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None).ToListAsync();

        Assert.Equal([first, lastMoment], rows.Select(r => r.Id));
    }

    private AdminOrderService Service(AppDbContext db, IEmailSender? emailSender = null, IInventoryStore? inventoryStore = null) =>
        new(db, new OrderLineInventory(inventoryStore ?? new EfInventoryStore(db)), new UnusedPaymentGateway(), emailSender ?? new RecordingEmailSender(), new FixedTimeProvider(Now), NullLogger<AdminOrderService>.Instance);

    private async Task<List<int>> SearchAsync(string search)
    {
        await using var db = database.CreateDb();
        var page = await Service(db).GetOrdersAsync(AdminOrderTab.All, new AdminOrderFilter(search), 1, 100, CancellationToken.None);
        return page.Orders.Select(o => o.Id).Order().ToList();
    }

    private static OrderLine Line(int productId, int quantity) => new()
    {
        ProductId = productId,
        ProductName = "Print",
        UnitPrice = 10m,
        Quantity = quantity,
    };

    private async Task<int> AddProductAsync(int stock)
    {
        await using var db = database.CreateDb();
        var slug = $"print-{Guid.NewGuid():N}";
        var product = new Product { Name = slug, Slug = slug, Description = "A print", Price = 10m, StockQuantity = stock };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<int> AddOrderAsync(
        string email = "shopper@example.com",
        string customerName = "Jane Shopper",
        DateTimeOffset? createdAt = null,
        List<OrderLine>? lines = null)
    {
        await using var db = database.CreateDb();
        var order = new Order
        {
            Email = email,
            FulfillmentMethod = FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = customerName },
            Lines = lines ?? [],
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Order> LoadAsync(int id)
    {
        await using var db = database.CreateDb();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private async Task<int> StockAsync(int productId)
    {
        await using var db = database.CreateDb();
        return await db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
