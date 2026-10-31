using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.UnitTests;

// Runs the abandoned checkout listing against an in-memory SQLite database with a frozen clock.
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

    private AbandonedCheckoutService Service() => new(database.CreateDb(), new FixedTimeProvider(Now));

    private async Task AddPendingCheckoutAsync(string email, DateTimeOffset createdAt, FulfillmentMethod fulfillment = FulfillmentMethod.Shipping, decimal subtotal = 20m, decimal shipping = 0m)
    {
        await using var db = database.CreateDb();
        db.PendingCheckouts.Add(new PendingCheckout
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
        });
        await db.SaveChangesAsync();
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
}
