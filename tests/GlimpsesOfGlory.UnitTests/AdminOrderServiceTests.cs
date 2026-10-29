using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.UnitTests;

// Runs AdminOrderService against an in-memory SQLite database, since its status changes are
// ExecuteUpdate calls that need a relational provider.
public sealed class AdminOrderServiceTests : IDisposable
{
    private static readonly DateTimeOffset T1 = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ManualTimeProvider clock = new(T1);

    public AdminOrderServiceTests()
    {
        connection.Open();
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    public void Dispose() => connection.Dispose();

    [Fact]
    public async Task MarkShipped_StampsShippedAt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        var result = await Service().MarkShippedAsync(id, "TRACK1", CancellationToken.None);

        Assert.Equal(OrderUpdateResult.Updated, result);
        var order = await LoadAsync(id);
        Assert.Equal(T1, order.ShippedAt);
        Assert.Null(order.PickedUpAt);
        Assert.Null(order.CancelledAt);
    }

    [Fact]
    public async Task MarkShipped_RepeatCall_DoesNotMoveShippedAt()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);
        await Service().MarkShippedAsync(id, null, CancellationToken.None);

        clock.Now = T2;
        var result = await Service().MarkShippedAsync(id, null, CancellationToken.None);

        Assert.Equal(OrderUpdateResult.InvalidTransition, result);
        Assert.Equal(T1, (await LoadAsync(id)).ShippedAt);
    }

    [Fact]
    public async Task MarkShipped_KeepsAnAlreadyRecordedShippedAt()
    {
        // The update coalesces, so even a call that gets past the status guard can't move the stamp.
        var id = await AddOrderAsync(FulfillmentMethod.Shipping, shippedAt: T1);
        clock.Now = T2;

        await Service().MarkShippedAsync(id, null, CancellationToken.None);

        Assert.Equal(T1, (await LoadAsync(id)).ShippedAt);
    }

    [Fact]
    public async Task MarkPickedUp_StampsPickedUpAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        await Service().MarkShippedAsync(id, null, CancellationToken.None);

        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkPickedUpAsync(id, CancellationToken.None));
        clock.Now = T2.AddDays(1);
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkPickedUpAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(T1, order.ShippedAt);
        Assert.Equal(T2, order.PickedUpAt);
    }

    [Fact]
    public async Task MarkPaid_StampsPaidAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);

        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkPaidAsync(id, CancellationToken.None));
        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkPaidAsync(id, CancellationToken.None));

        Assert.Equal(T1, (await LoadAsync(id)).PaidAt);
    }

    [Fact]
    public async Task MarkCancelled_StampsCancelledAt_Once()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Shipping);

        Assert.Equal(OrderUpdateResult.Updated, await Service().MarkCancelledAsync(id, CancellationToken.None));
        clock.Now = T2;
        Assert.Equal(OrderUpdateResult.InvalidTransition, await Service().MarkCancelledAsync(id, CancellationToken.None));

        var order = await LoadAsync(id);
        Assert.Equal(T1, order.CancelledAt);
        Assert.Null(order.ShippedAt);
    }

    [Fact]
    public async Task GetOrder_ExposesTheStepTimestamps()
    {
        var id = await AddOrderAsync(FulfillmentMethod.Pickup);
        await Service().MarkShippedAsync(id, null, CancellationToken.None);
        clock.Now = T2;
        await Service().MarkPickedUpAsync(id, CancellationToken.None);

        var detail = await Service().GetOrderAsync(id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(T1, detail.ShippedAt);
        Assert.Equal(T2, detail.PickedUpAt);
        Assert.Null(detail.PaidAt);
        Assert.Null(detail.CancelledAt);
    }

    [Fact]
    public async Task GetOrders_FiltersByTab_AndCountsMatch()
    {
        var newCash = await AddOrderAsync(FulfillmentMethod.Shipping);
        var newCard = await AddOrderAsync(FulfillmentMethod.Shipping, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var shippedUnpaidCash = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped);
        var shippedPaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paymentMethod: PaymentMethod.Card, paidAt: T1);
        var pickedUpPaid = await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.PickedUp, paidAt: T1);
        var cancelledUnpaid = await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);
        var service = Service();

        Assert.Equal([newCash, newCard, shippedUnpaidCash], await IdsAsync(service, AdminOrderTab.NeedsAction));
        Assert.Equal([shippedUnpaidCash, shippedPaid, pickedUpPaid], await IdsAsync(service, AdminOrderTab.Shipped));
        Assert.Equal([cancelledUnpaid], await IdsAsync(service, AdminOrderTab.Cancelled));
        Assert.Equal(6, (await IdsAsync(service, AdminOrderTab.All)).Count);

        Assert.Equal(new AdminOrderTabCounts(NeedsAction: 3, Shipped: 3, Cancelled: 1, All: 6), await service.GetOrderTabCountsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetOrders_NeedsActionTab_AgreesWithTheRowHighlight()
    {
        await AddOrderAsync(FulfillmentMethod.Shipping);
        await AddOrderAsync(FulfillmentMethod.Pickup, status: OrderStatus.Shipped);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Shipped, paidAt: T1);
        await AddOrderAsync(FulfillmentMethod.Shipping, status: OrderStatus.Cancelled);

        var all = await Service().GetOrdersAsync(AdminOrderTab.All, CancellationToken.None);
        var needsAction = await IdsAsync(Service(), AdminOrderTab.NeedsAction);

        Assert.Equal(all.Where(o => o.NeedsAction).Select(o => o.Id).Order(), needsAction);
    }

    [Fact]
    public async Task GetOrders_ListsNewestFirst()
    {
        var older = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: T1);
        var newer = await AddOrderAsync(FulfillmentMethod.Shipping, createdAt: T2);

        var orders = await Service().GetOrdersAsync(AdminOrderTab.NeedsAction, CancellationToken.None);

        Assert.Equal([newer, older], orders.Select(o => o.Id));
    }

    [Fact]
    public async Task GetOrderTabCounts_WithNoOrders_IsAllZero()
    {
        Assert.Equal(new AdminOrderTabCounts(0, 0, 0, 0), await Service().GetOrderTabCountsAsync(CancellationToken.None));
    }

    private static async Task<List<int>> IdsAsync(AdminOrderService service, AdminOrderTab tab)
    {
        var orders = await service.GetOrdersAsync(tab, CancellationToken.None);
        // Same CreatedAt for most seeded orders, so compare by id to stay order-independent.
        return orders.Select(o => o.Id).Order().ToList();
    }

    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>()
            .Options);

    // SQLite can't ORDER BY a DateTimeOffset (production runs on Postgres, which can), so the test
    // model stores them as sortable binary values instead.
    private sealed class SqliteDateTimeOffsetCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }

    private AdminOrderService Service() =>
        new(CreateDb(), new FakeInventoryStore(new Dictionary<int, int>()), new FakePaymentGateway(), new NullEmailSender(), clock, NullLogger<AdminOrderService>.Instance);

    private async Task<int> AddOrderAsync(
        FulfillmentMethod fulfillmentMethod,
        DateTimeOffset? shippedAt = null,
        OrderStatus status = OrderStatus.New,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        DateTimeOffset? paidAt = null,
        DateTimeOffset? createdAt = null)
    {
        await using var db = CreateDb();
        var order = new Order
        {
            Email = "shopper@example.com",
            FulfillmentMethod = fulfillmentMethod,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Status = status,
            PaymentMethod = paymentMethod,
            PaidAt = paidAt,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
            ShippedAt = shippedAt,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Order> LoadAsync(int id)
    {
        await using var db = CreateDb();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public Task<PaymentIntentSetup> CreatePaymentIntentAsync(decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PaymentIntentSetup> UpdatePaymentIntentAsync(string paymentIntentId, decimal amount, string? receiptEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public PaymentWebhookResult HandleWebhookEvent(string payload, string signatureHeader) =>
            throw new NotSupportedException();

        public string GetPaymentDashboardUrl(string paymentIntentId) => $"https://dashboard.example/{paymentIntentId}";
    }
}
