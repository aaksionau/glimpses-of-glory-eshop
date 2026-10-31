using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.UnitTests;

// Stripe confirms a payment, but the stock sold out between creating the PaymentIntent and the
// webhook arriving: no order can be made, so the shortfall must be logged for a manual refund.
public sealed class CardPaymentStockRanOutTests : IDisposable
{
    private const string PaymentIntentId = "pi_short";

    private readonly SqliteTestDatabase database = new();
    private readonly CapturingLogger<OrderService> logger = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task ConfirmPayment_WhenStockRanOut_LogsAnErrorNamingThePaymentBuyerAndShortProduct()
    {
        await SeedPaidCheckoutAsync();

        // Completing normally is what keeps the webhook at 200, so Stripe doesn't retry.
        await OrderService().ConfirmPaymentAsync(PaymentIntentId, CancellationToken.None);

        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(PaymentIntentId, error.Message);
        Assert.Contains("shopper@example.com", error.Message);
        Assert.Contains("Sunrise", error.Message);
        Assert.Contains("wanted 3, 1 left", error.Message);
        // Harbour had enough stock, so it isn't named as short.
        Assert.DoesNotContain("Harbour", error.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenStockRanOut_LeavesThePendingCheckoutAndStockUntouched()
    {
        await SeedPaidCheckoutAsync();

        await OrderService().ConfirmPaymentAsync(PaymentIntentId, CancellationToken.None);

        await using var check = database.CreateDb();
        Assert.Empty(check.Orders);
        var pending = await check.PendingCheckouts.Include(p => p.Lines).SingleAsync(p => p.StripePaymentIntentId == PaymentIntentId);
        Assert.Equal("shopper@example.com", pending.Email);
        Assert.Equal(2, pending.Lines.Count);
        // Harbour is reserved before Sunrise fails; the rollback must put it back.
        Assert.Equal(5, check.Products.Single(p => p.Slug == "harbour").StockQuantity);
        Assert.Equal(1, check.Products.Single(p => p.Slug == "sunrise").StockQuantity);
    }

    // Harbour (5 in stock) wants 1 and is fine; Sunrise had stock at checkout but only 1 is left now.
    private async Task SeedPaidCheckoutAsync()
    {
        await using var db = database.CreateDb();
        var harbour = new Product { Name = "Harbour", Slug = "harbour", Description = "d", Price = 15m, StockQuantity = 5 };
        var sunrise = new Product { Name = "Sunrise", Slug = "sunrise", Description = "d", Price = 20m, StockQuantity = 1 };
        db.Products.AddRange(harbour, sunrise);
        await db.SaveChangesAsync();

        db.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = PaymentIntentId,
            Email = "shopper@example.com",
            FulfillmentMethod = FulfillmentMethod.Pickup,
            ShippingAddress = ShippingAddress.FromInfo(new ShippingAddressInfo("shopper@example.com", "Sam Shopper", null, null, null, null, null, null)),
            Subtotal = 75m,
            ShippingCost = 0m,
            Lines =
            [
                new PendingCheckoutLine { ProductId = harbour.Id, ProductName = "Harbour", UnitPrice = 15m, Quantity = 1 },
                new PendingCheckoutLine { ProductId = sunrise.Id, ProductName = "Sunrise", UnitPrice = 20m, Quantity = 3 },
            ],
        });
        await db.SaveChangesAsync();
    }

    private OrderService OrderService()
    {
        var db = database.CreateDb();
        return new(
            db,
            new StubCartService(new CartSummary([], 0m, 0m)),
            new FakePaymentGateway(),
            new OrderLineInventory(new EfInventoryStore(db)),
            new NullEmailSender(),
            TimeProvider.System,
            logger);
    }
}
