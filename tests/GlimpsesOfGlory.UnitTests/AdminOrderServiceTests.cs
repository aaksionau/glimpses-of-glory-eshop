using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    private AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private AdminOrderService Service() =>
        new(CreateDb(), new FakeInventoryStore(new Dictionary<int, int>()), new FakePaymentGateway(), new NullEmailSender(), clock, NullLogger<AdminOrderService>.Instance);

    private async Task<int> AddOrderAsync(FulfillmentMethod fulfillmentMethod, DateTimeOffset? shippedAt = null)
    {
        await using var db = CreateDb();
        var order = new Order
        {
            Email = "shopper@example.com",
            FulfillmentMethod = fulfillmentMethod,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            PaymentMethod = PaymentMethod.Cash,
            CreatedAt = new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
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
