using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.IntegrationTests;

// Restores a reminder's Pending Checkout into a cart, against a migrated Postgres database.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CartRestoreServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Token = "restore-token";
    private const string PaymentIntentId = "pi_restored";

    private TestDatabase database = null!;
    private readonly FakeCartStore cartStore = new();

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Restore_IntoAnEmptyCart_AddsTheStoredLines()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        var card = await AddProductAsync("card", "Card", 4m);
        await AddPendingCheckoutAsync((candle, 2), (card, 1));

        var result = await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.NotNull(result.PaymentIntentId);
        Assert.Equal(0, result.SkippedLineCount);
        Assert.Equal([("candle", 2), ("card", 1)], cartStore.Cart.Lines.Select(l => (l.ProductSlug, l.Quantity)));
    }

    [Fact]
    public async Task Restore_ReturnsThePendingCheckoutsPaymentIntentId()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        await AddPendingCheckoutAsync((candle, 1));

        var result = await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.Equal(PaymentIntentId, result.PaymentIntentId);
    }

    [Fact]
    public async Task Restore_UsesTheCurrentProductData_NotTheSnapshot()
    {
        var candle = await AddProductAsync("candle-renamed", "Beeswax candle", 12.50m, photo: "candle.jpg", isPreorder: true, expected: new DateOnly(2026, 12, 1));
        await AddPendingCheckoutAsync((candle, 2));

        await Service().RestoreAsync(Token, CancellationToken.None);

        var line = Assert.Single(cartStore.Cart.Lines);
        Assert.Equal("candle-renamed", line.ProductSlug);
        Assert.Equal("Beeswax candle", line.ProductName);
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal("candle.jpg", line.ThumbnailFileName);
        Assert.True(line.IsPreorder);
        Assert.Equal(new DateOnly(2026, 12, 1), line.ExpectedAvailabilityDate);
    }

    [Fact]
    public async Task Restore_KeepsTheLargerOfTheCurrentAndStoredQuantities()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        var card = await AddProductAsync("card", "Card", 4m);
        await AddPendingCheckoutAsync((candle, 2), (card, 3));
        AddToCart("candle", 5);
        AddToCart("card", 1);

        await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.Equal([("candle", 5), ("card", 3)], cartStore.Cart.Lines.Select(l => (l.ProductSlug, l.Quantity)));
    }

    [Fact]
    public async Task Restore_Twice_ChangesNothingTheSecondTime()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        await AddPendingCheckoutAsync((candle, 2));

        await Service().RestoreAsync(Token, CancellationToken.None);
        await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.Equal([("candle", 2)], cartStore.Cart.Lines.Select(l => (l.ProductSlug, l.Quantity)));
    }

    [Fact]
    public async Task Restore_KeepsLinesThatAreOnlyInTheCurrentCart()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        await AddPendingCheckoutAsync((candle, 1));
        AddToCart("mug", 2);

        await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.Equal([("mug", 2), ("candle", 1)], cartStore.Cart.Lines.Select(l => (l.ProductSlug, l.Quantity)));
    }

    [Fact]
    public async Task Restore_SkipsAndCountsDeletedAndInactiveProducts()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        var retired = await AddProductAsync("retired", "Retired print", 30m, isActive: false);
        const int deletedProductId = 999;
        await AddPendingCheckoutAsync((candle, 1), (retired, 1), (deletedProductId, 1));

        var result = await Service().RestoreAsync(Token, CancellationToken.None);

        Assert.NotNull(result.PaymentIntentId);
        Assert.Equal(2, result.SkippedLineCount);
        Assert.Equal(["candle"], cartStore.Cart.Lines.Select(l => l.ProductSlug));
    }

    [Fact]
    public async Task Restore_UnknownToken_ReportsExpired_AndLeavesTheCartAlone()
    {
        var candle = await AddProductAsync("candle", "Candle", 10m);
        await AddPendingCheckoutAsync((candle, 1));
        AddToCart("mug", 2);

        var result = await Service().RestoreAsync("not-a-token", CancellationToken.None);

        Assert.Equal(CartRestoreResult.Expired(), result);
        Assert.Equal([("mug", 2)], cartStore.Cart.Lines.Select(l => (l.ProductSlug, l.Quantity)));
    }

    private CartRestoreService Service()
    {
        var db = database.CreateDb();
        return new(db, new ProductCatalogService(db), cartStore);
    }

    private void AddToCart(string slug, int quantity) =>
        cartStore.Cart.SetLineQuantity(slug, slug, 1m, null, quantity, false, null);

    private async Task<int> AddProductAsync(string slug, string name, decimal price, string? photo = null, bool isActive = true, bool isPreorder = false, DateOnly? expected = null)
    {
        await using var db = database.CreateDb();
        var product = new Product
        {
            Slug = slug,
            Name = name,
            Description = name,
            Price = price,
            StockQuantity = 10,
            IsActive = isActive,
            IsPreorder = isPreorder,
            ExpectedAvailabilityDate = expected,
            Photos = photo is null ? [] : [new ProductPhoto { FileName = photo }],
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    // The stored lines carry a stale name and price, which a restore must not use.
    private async Task AddPendingCheckoutAsync(params (int ProductId, int Quantity)[] lines)
    {
        await using var db = database.CreateDb();
        db.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = PaymentIntentId,
            Email = "shopper@example.com",
            FulfillmentMethod = FulfillmentMethod.Shipping,
            ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
            Subtotal = 20m,
            RestoreToken = Token,
            Lines = lines.Select(l => new PendingCheckoutLine { ProductId = l.ProductId, ProductName = "Old name", UnitPrice = 1m, Quantity = l.Quantity }).ToList(),
        });
        await db.SaveChangesAsync();
    }
}
