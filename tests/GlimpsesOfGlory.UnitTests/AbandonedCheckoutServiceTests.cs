using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.UnitTests;

// Runs the abandoned checkout listing and reminder against an in-memory SQLite database with a frozen clock.
public sealed class AbandonedCheckoutServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task List_IncludesOnlyCheckoutsBetweenOneHourAndThirtyDaysOld()
    {
        await AddPendingCheckoutAsync("too-new@example.com", Now.AddMinutes(-59));
        await AddPendingCheckoutAsync("just-over-hour@example.com", Now.AddMinutes(-61));
        await AddPendingCheckoutAsync("just-under-30-days@example.com", Now.AddDays(-30).AddMinutes(1));
        await AddPendingCheckoutAsync("too-old@example.com", Now.AddDays(-30).AddMinutes(-1));

        var listed = await Service().GetAbandonedCheckoutsAsync(CancellationToken.None);

        Assert.Equal(["just-over-hour@example.com", "just-under-30-days@example.com"], listed.Select(c => c.Email));
    }

    [Fact]
    public async Task List_HidesCheckoutWhenSameEmailOrderedAfterIt_ButNotBefore()
    {
        await AddPendingCheckoutAsync("converted@example.com", Now.AddDays(-2));
        await AddOrderAsync("converted@example.com", Now.AddDays(-1));
        await AddPendingCheckoutAsync("ordered-earlier@example.com", Now.AddDays(-2));
        await AddOrderAsync("ordered-earlier@example.com", Now.AddDays(-3));

        var listed = await Service().GetAbandonedCheckoutsAsync(CancellationToken.None);

        Assert.Equal(["ordered-earlier@example.com"], listed.Select(c => c.Email));
    }

    [Fact]
    public async Task List_IsNewestFirst()
    {
        await AddPendingCheckoutAsync("middle@example.com", Now.AddDays(-5));
        await AddPendingCheckoutAsync("newest@example.com", Now.AddHours(-2));
        await AddPendingCheckoutAsync("oldest@example.com", Now.AddDays(-20));

        var listed = await Service().GetAbandonedCheckoutsAsync(CancellationToken.None);

        Assert.Equal(["newest@example.com", "middle@example.com", "oldest@example.com"], listed.Select(c => c.Email));
    }

    [Fact]
    public async Task List_CarriesLinesTotalAndFulfillment()
    {
        await AddPendingCheckoutAsync("shopper@example.com", Now.AddHours(-3), FulfillmentMethod.Pickup, subtotal: 30m, shipping: 5m);

        var checkout = Assert.Single(await Service().GetAbandonedCheckoutsAsync(CancellationToken.None));

        Assert.Equal([("Candle", 2), ("Card", 1)], checkout.Lines.Select(l => (l.ProductName, l.Quantity)));
        Assert.Equal(35m, checkout.Total);
        Assert.Equal(FulfillmentMethod.Pickup, checkout.FulfillmentMethod);
        Assert.Equal(Now.AddHours(-3), checkout.CreatedAt);
    }

    [Fact]
    public async Task SendReminder_EmailsTheShopper_ThenStampsRemindedAtAndToken()
    {
        var id = await AddPendingCheckoutAsync("shopper@example.com", Now.AddHours(-3));
        var emailSender = new RecordingEmailSender();

        var result = await Service(emailSender).SendReminderAsync(id, "https://shop.example/", CancellationToken.None);

        Assert.Equal(ReminderSendResult.Sent, result);
        var stored = await LoadAsync(id);
        Assert.Equal(Now, stored.RemindedAt);
        Assert.NotNull(stored.RestoreToken);
        // 32 random bytes, base64url without padding.
        Assert.Equal(43, stored.RestoreToken.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", stored.RestoreToken);

        var sent = Assert.Single(emailSender.Reminders);
        Assert.Equal("shopper@example.com", sent.Email);
        Assert.Equal([("Candle", 2), ("Card", 1)], sent.Lines.Select(l => (l.ProductName, l.Quantity)));
        Assert.Equal($"https://shop.example/cart/restore/{stored.RestoreToken}", sent.RestoreUrl);
    }

    [Fact]
    public async Task SendReminder_GeneratesADifferentTokenPerCheckout()
    {
        var first = await AddPendingCheckoutAsync("first@example.com", Now.AddHours(-3));
        var second = await AddPendingCheckoutAsync("second@example.com", Now.AddHours(-3));

        await Service().SendReminderAsync(first, "https://shop.example", CancellationToken.None);
        await Service().SendReminderAsync(second, "https://shop.example", CancellationToken.None);

        Assert.NotEqual((await LoadAsync(first)).RestoreToken, (await LoadAsync(second)).RestoreToken);
    }

    [Fact]
    public async Task SendReminder_WhenAlreadyReminded_SendsNothing_AndKeepsTheFirstToken()
    {
        var id = await AddPendingCheckoutAsync("shopper@example.com", Now.AddHours(-3));
        await Service().SendReminderAsync(id, "https://shop.example", CancellationToken.None);
        var firstToken = (await LoadAsync(id)).RestoreToken;
        var emailSender = new RecordingEmailSender();

        var result = await Service(emailSender).SendReminderAsync(id, "https://shop.example", CancellationToken.None);

        Assert.Equal(ReminderSendResult.AlreadyReminded, result);
        Assert.Empty(emailSender.Reminders);
        Assert.Equal(firstToken, (await LoadAsync(id)).RestoreToken);
    }

    [Fact]
    public async Task SendReminder_UnknownCheckout_ReportsNotFound()
    {
        var emailSender = new RecordingEmailSender();

        var result = await Service(emailSender).SendReminderAsync(999, "https://shop.example", CancellationToken.None);

        Assert.Equal(ReminderSendResult.NotFound, result);
        Assert.Empty(emailSender.Reminders);
    }

    [Fact]
    public async Task SendReminder_WhenTheEmailFails_ReportsFailed_AndStampsNothing()
    {
        var id = await AddPendingCheckoutAsync("shopper@example.com", Now.AddHours(-3));

        var result = await Service(new RecordingEmailSender { Fail = true }).SendReminderAsync(id, "https://shop.example", CancellationToken.None);

        Assert.Equal(ReminderSendResult.Failed, result);
        var stored = await LoadAsync(id);
        Assert.Null(stored.RemindedAt);
        Assert.Null(stored.RestoreToken);
    }

    [Fact]
    public async Task List_CarriesTheRemindedState()
    {
        var reminded = await AddPendingCheckoutAsync("reminded@example.com", Now.AddHours(-3));
        await AddPendingCheckoutAsync("not-reminded@example.com", Now.AddHours(-4));
        await Service().SendReminderAsync(reminded, "https://shop.example", CancellationToken.None);

        var listed = await Service().GetAbandonedCheckoutsAsync(CancellationToken.None);

        Assert.Equal([Now, null], listed.Select(c => c.RemindedAt));
    }

    private AbandonedCheckoutService Service(IEmailSender? emailSender = null) =>
        new(database.CreateDb(), emailSender ?? new NullEmailSender(), new FixedTimeProvider(Now), NullLogger<AbandonedCheckoutService>.Instance);

    private async Task<PendingCheckout> LoadAsync(int id)
    {
        await using var db = database.CreateDb();
        return await db.PendingCheckouts.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    private async Task<int> AddPendingCheckoutAsync(string email, DateTimeOffset createdAt, FulfillmentMethod fulfillment = FulfillmentMethod.Shipping, decimal subtotal = 20m, decimal shipping = 0m)
    {
        await using var db = database.CreateDb();
        var checkout = new PendingCheckout
        {
            StripePaymentIntentId = $"pi_{Guid.NewGuid():N}",
            Email = email,
            FulfillmentMethod = fulfillment,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Subtotal = subtotal,
            ShippingCost = shipping,
            CreatedAt = createdAt,
            Lines =
            [
                new PendingCheckoutLine { ProductId = 1, ProductName = "Candle", UnitPrice = 10m, Quantity = 2 },
                new PendingCheckoutLine { ProductId = 2, ProductName = "Card", UnitPrice = 10m, Quantity = 1 },
            ],
        };
        db.PendingCheckouts.Add(checkout);
        await db.SaveChangesAsync();
        return checkout.Id;
    }

    private async Task AddOrderAsync(string email, DateTimeOffset createdAt)
    {
        await using var db = database.CreateDb();
        db.Orders.Add(new Order
        {
            Email = email,
            FulfillmentMethod = FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Status = OrderStatus.New,
            PaymentMethod = PaymentMethod.Card,
            Subtotal = 20m,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync();
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<AbandonedCheckoutReminderView> Reminders { get; } = [];

        // Simulates the email provider rejecting the send.
        public bool Fail { get; init; }

        public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendReadyForPickupNotificationAsync(OrderReadyForPickupView order, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendAbandonedCheckoutReminderAsync(AbandonedCheckoutReminderView reminder, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new HttpRequestException("Resend rejected email (500)");
            }

            Reminders.Add(reminder);
            return Task.CompletedTask;
        }
    }
}
