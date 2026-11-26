using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Infrastructure.Options;
using GlimpsesOfGlory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// Saving the admin edit form must not put back stock an order took while the form was open.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AdminProductUpdateStockTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Save_WithStockUntouched_AfterAnOrderTookStock_KeepsTheLoweredStock()
    {
        var id = await AddProductAsync(stock: 5);
        await SellAsync(id, 1); // an order comes in while the form (loaded at 5) is open

        var result = await Service().UpdateProductAsync(id, Request(price: 12m, stock: 5, loaded: 5), CancellationToken.None);

        Assert.Equal(ProductUpdateResult.Updated, result);
        var product = await ReadAsync(id);
        Assert.Equal(12m, product.Price);
        Assert.Equal(4, product.StockQuantity);
    }

    [Fact]
    public async Task Save_WithStockChanged_WhenNothingElseMovedIt_WritesTheNewStock()
    {
        var id = await AddProductAsync(stock: 5);

        var result = await Service().UpdateProductAsync(id, Request(price: 12m, stock: 9, loaded: 5), CancellationToken.None);

        Assert.Equal(ProductUpdateResult.Updated, result);
        Assert.Equal(9, (await ReadAsync(id)).StockQuantity);
    }

    [Fact]
    public async Task Save_WithStockChanged_AfterAnOrderTookStock_SavesNothing()
    {
        var id = await AddProductAsync(stock: 5);
        await SellAsync(id, 1);

        var result = await Service().UpdateProductAsync(id, Request(price: 12m, stock: 9, loaded: 5), CancellationToken.None);

        Assert.Equal(ProductUpdateResult.StockChanged, result);
        var product = await ReadAsync(id);
        Assert.Equal(10m, product.Price);
        Assert.Equal(4, product.StockQuantity);
    }

    [Fact]
    public async Task Save_UnknownProduct_IsNotFound()
    {
        var result = await Service().UpdateProductAsync(999, Request(price: 12m, stock: 9, loaded: 5), CancellationToken.None);

        Assert.Equal(ProductUpdateResult.NotFound, result);
    }

    private AdminProductService Service() =>
        new(database.CreateDb(), new ProductPhotoStorage(new ProductPhotoStorageOptions { StoragePath = Path.GetTempPath() }));

    private static ProductEditRequest Request(decimal price, int stock, int loaded) =>
        new("Print", "A print", price, stock, false, null, null, loaded);

    private async Task<int> AddProductAsync(int stock)
    {
        await using var db = database.CreateDb();
        var product = new Product { Name = "Print", Slug = "print", Description = "A print", Price = 10m, StockQuantity = stock };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task SellAsync(int productId, int quantity)
    {
        await using var db = database.CreateDb();
        Assert.True(await new EfInventoryStore(db).TryReserveStockAsync(productId, quantity, CancellationToken.None));
    }

    private async Task<Product> ReadAsync(int productId)
    {
        await using var db = database.CreateDb();
        return await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }
}
