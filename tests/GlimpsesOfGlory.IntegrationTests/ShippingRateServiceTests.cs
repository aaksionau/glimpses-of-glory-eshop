using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.IntegrationTests;

// Quotes shipping from each product's own rate or the store default, against a migrated Postgres database.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ShippingRateServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task DefaultRate_IsSeededByTheMigration()
    {
        var rate = await Service().GetDefaultRateAsync(CancellationToken.None);

        Assert.Equal(new ShippingRateView(5.00m, 1.00m), rate);
    }

    [Fact]
    public async Task UpdateDefaultRate_IsReadBack()
    {
        await Service().UpdateDefaultRateAsync(new ShippingRateView(6.50m, 2.25m), CancellationToken.None);

        Assert.Equal(new ShippingRateView(6.50m, 2.25m), await Service().GetDefaultRateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Quote_ProductWithoutItsOwnRate_UsesTheDefault()
    {
        await AddProductAsync("card");

        var cost = await Service().QuoteAsync([new ShippingQuoteLine("card", 3)], CancellationToken.None);

        Assert.Equal(7.00m, cost); // $5 + 2 x $1
    }

    [Fact]
    public async Task Quote_MixesOwnRatesAndTheDefault()
    {
        await AddProductAsync("card");
        await AddProductAsync("mug", firstItem: 8.00m, additionalItem: 3.00m);

        var cost = await Service().QuoteAsync(
            [new ShippingQuoteLine("card", 2), new ShippingQuoteLine("mug", 1)],
            CancellationToken.None);

        Assert.Equal(10.00m, cost); // mug opens at $8, two cards at the default $1 each
    }

    [Fact]
    public async Task Quote_UnknownProduct_UsesTheDefault()
    {
        var cost = await Service().QuoteAsync([new ShippingQuoteLine("gone", 1)], CancellationToken.None);

        Assert.Equal(5.00m, cost);
    }

    [Fact]
    public async Task Quote_EmptyCart_IsFree()
    {
        Assert.Equal(0m, await Service().QuoteAsync([], CancellationToken.None));
    }

    private ShippingRateService Service() => new(database.CreateDb());

    private async Task AddProductAsync(string slug, decimal? firstItem = null, decimal? additionalItem = null)
    {
        await using var db = database.CreateDb();
        db.Products.Add(new Product
        {
            Slug = slug,
            Name = slug,
            Description = slug,
            Price = 10m,
            StockQuantity = 10,
            ShippingFirstItemAmount = firstItem,
            ShippingAdditionalItemAmount = additionalItem,
        });
        await db.SaveChangesAsync();
    }
}
