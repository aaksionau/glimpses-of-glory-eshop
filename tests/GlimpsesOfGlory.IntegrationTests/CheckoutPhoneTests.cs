using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlimpsesOfGlory.IntegrationTests;

// The optional phone given at checkout survives the PendingCheckout copy and reaches the admin.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CheckoutPhoneTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    // Card orders wait in a PendingCheckout until Stripe confirms payment; the phone must survive that copy.
    [Fact]
    public async Task CardOrder_CarriesThePhoneFromThePendingCheckout()
    {
        var productId = await SeedProductAsync();
        await using (var db = database.CreateDb())
        {
            db.PendingCheckouts.Add(new PendingCheckout
            {
                StripePaymentIntentId = "pi_phone",
                Email = "shopper@example.com",
                FulfillmentMethod = FulfillmentMethod.Shipping,
                ShippingAddress = ShippingAddress.FromInfo(new ShippingAddressInfo(
                    "shopper@example.com", "Sam Shopper", "1 Main St", null, "Springfield", "IL", "62701", "United States", Phone: "+1 555 0100")),
                Subtotal = 20m,
                ShippingCost = 8m,
                Lines = [new PendingCheckoutLine { ProductId = productId, ProductName = "Sunrise", UnitPrice = 20m, Quantity = 1 }],
            });
            await db.SaveChangesAsync();
        }

        await OrderService().ConfirmPaymentAsync("pi_phone", CancellationToken.None);

        await using var check = database.CreateDb();
        var orderId = check.Orders.Single(o => o.StripePaymentIntentId == "pi_phone").Id;
        var detail = await AdminOrderService().GetOrderAsync(orderId, CancellationToken.None);
        Assert.Equal("+1 555 0100", detail!.Address.Phone);
    }

    private async Task<int> SeedProductAsync()
    {
        await using var db = database.CreateDb();
        var product = new Product { Name = "Sunrise", Slug = "sunrise", Description = "d", Price = 20m, StockQuantity = 5 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private OrderService OrderService()
    {
        var db = database.CreateDb();
        var cart = new CartSummary([new CartLineView("sunrise", "Sunrise", null, 20m, 1, false, null)], 20m, 8m);
        return new(
            db,
            new StubCartService(cart),
            new FakePaymentGateway(),
            new OrderLineInventory(new EfInventoryStore(db)),
            new NullEmailSender(),
            TimeProvider.System,
            NullLogger<OrderService>.Instance);
    }

    private AdminOrderService AdminOrderService()
    {
        var db = database.CreateDb();
        return new(db, new OrderLineInventory(new EfInventoryStore(db)), new FakePaymentGateway(), new NullEmailSender(), TimeProvider.System, NullLogger<AdminOrderService>.Instance);
    }
}
