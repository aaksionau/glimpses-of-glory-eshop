using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.IntegrationTests;

// Pricing the cart against the catalogue, and the one stock rule both checkout paths share.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CheckoutPricerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Price_RepricesEachLineFromTheCatalogue_NotTheSessionCart()
    {
        await SeedProductsAsync();

        var pricing = await Pricer(Cart(("sunrise", 2), ("harbour", 1))).PriceAsync(FulfillmentMethod.Pickup, CancellationToken.None);

        var priced = Assert.IsType<PricedCart>(pricing.Priced);
        Assert.Empty(pricing.UnavailableLines);
        Assert.Equal([20m, 15m], priced.Lines.Select(l => l.UnitPrice));
        Assert.Equal([20m, 15m], priced.Cart.Lines.Select(l => l.UnitPrice));
        Assert.Equal(55m, priced.Subtotal);
    }

    [Fact]
    public async Task Price_WhenALineIsShort_ListsItWithHowManyAreLeft_AndPricesNothing()
    {
        await SeedProductsAsync();

        var pricing = await Pricer(Cart(("sunrise", 3), ("gone", 1), ("meadow", 5))).PriceAsync(FulfillmentMethod.Pickup, CancellationToken.None);

        Assert.Null(pricing.Priced);
        Assert.Equal([new UnavailableCartLine("sunrise", 2), new UnavailableCartLine("gone", 0)], pricing.UnavailableLines);
    }

    [Fact]
    public async Task Price_WhenTheCartIsEmpty_PricesNothing_AndListsNoLines()
    {
        var pricing = await Pricer(Cart()).PriceAsync(FulfillmentMethod.Pickup, CancellationToken.None);

        Assert.Null(pricing.Priced);
        Assert.Empty(pricing.UnavailableLines);
    }

    [Fact]
    public async Task FindShortfalls_SumsLinesOfOneProduct_AndSkipsPreorderLines()
    {
        var (sunrise, harbour, meadow) = await SeedProductsAsync();
        ICheckoutLine[] lines =
        [
            new PendingCheckoutLine { ProductId = sunrise.Id, ProductName = "Sunrise", Quantity = 1 },
            new PendingCheckoutLine { ProductId = sunrise.Id, ProductName = "Sunrise", Quantity = 2 },
            new PendingCheckoutLine { ProductId = harbour.Id, ProductName = "Harbour", Quantity = 1 },
            new PendingCheckoutLine { ProductId = meadow.Id, ProductName = "Meadow", Quantity = 4, IsPreorder = true },
        ];

        var shortfalls = await Pricer(Cart()).FindShortfallsAsync(lines, CancellationToken.None);

        Assert.Equal([new StockShortfall(sunrise.Id, "Sunrise", Wanted: 3, Left: 2)], shortfalls);
    }

    // sunrise: 2 in stock at 20; harbour: 5 in stock at 15; meadow: sold out; "gone" isn't in the catalogue.
    private async Task<(Product Sunrise, Product Harbour, Product Meadow)> SeedProductsAsync()
    {
        await using var db = database.CreateDb();
        var sunrise = new Product { Name = "Sunrise", Slug = "sunrise", Description = "d", Price = 20m, StockQuantity = 2 };
        var harbour = new Product { Name = "Harbour", Slug = "harbour", Description = "d", Price = 15m, StockQuantity = 5 };
        var meadow = new Product { Name = "Meadow", Slug = "meadow", Description = "d", Price = 10m, StockQuantity = 0, IsPreorder = true };
        db.Products.AddRange(sunrise, harbour, meadow);
        await db.SaveChangesAsync();
        return (sunrise, harbour, meadow);
    }

    private CheckoutPricer Pricer(CartSummary cart) => new(database.CreateDb(), new StubCartService(cart));

    // Session prices are deliberately stale (10 each), so repricing is visible.
    private static CartSummary Cart(params (string Slug, int Quantity)[] lines) =>
        new(lines.Select(l => new CartLineView(l.Slug, l.Slug, null, 10m, l.Quantity, false, null)).ToList(), lines.Sum(l => 10m * l.Quantity), 8m);
}
