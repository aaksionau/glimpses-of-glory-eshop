using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

public class CartServiceTests
{
    private sealed class FakeProductCatalogService(ProductDetail? product) : IProductCatalogService
    {
        public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProductSummary>>([]);

        public Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken)
            => Task.FromResult(product);

        public Task<IReadOnlyDictionary<int, ProductSummary>> GetProductsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    // Quotes every unit at the store default: FirstItem for the first, AdditionalItem for each other.
    private sealed class FakeShippingRateService(decimal firstItem = 7.50m, decimal additionalItem = 0m) : IShippingRateService
    {
        public Task<ShippingRateView> GetDefaultRateAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ShippingRateView(firstItem, additionalItem));

        public Task UpdateDefaultRateAsync(ShippingRateView rate, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<decimal> QuoteAsync(IReadOnlyCollection<ShippingQuoteLine> lines, CancellationToken cancellationToken)
        {
            var quantity = lines.Sum(l => l.Quantity);
            return Task.FromResult(quantity <= 0 ? 0m : firstItem + additionalItem * (quantity - 1));
        }
    }

    private static ProductDetail CreateProduct(int stockQuantity) =>
        new("widget", "Widget", "A widget", 10.00m, stockQuantity, false, null, []);

    private static CartService CreateService(FakeCartStore cartStore, ProductDetail? product) =>
        new(cartStore, new FakeProductCatalogService(product), new FakeShippingRateService());

    // $8 for the first item, $2 for each additional one.
    private static CartService CreateRatedService(FakeCartStore cartStore, ProductDetail? product) =>
        new(cartStore, new FakeProductCatalogService(product), new FakeShippingRateService(8.00m, 2.00m));

    [Fact]
    public async Task GetSummaryAsync_ShippingCostFollowsQuantityChange()
    {
        var cartStore = new FakeCartStore();
        var service = CreateRatedService(cartStore, CreateProduct(stockQuantity: 10));
        await service.AddLineAsync("widget", 1, CancellationToken.None);

        await service.UpdateLineQuantityAsync("widget", 3, CancellationToken.None);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(12.00m, summary.ShippingCost);
    }

    [Fact]
    public async Task GetSummaryAsync_ShippingCostFor_IsFree_WhenPickup()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 5));
        await service.AddLineAsync("widget", 1, CancellationToken.None);

        var cost = (await service.GetSummaryAsync(CancellationToken.None)).ShippingCostFor(FulfillmentMethod.Pickup);

        Assert.Equal(0m, cost);
    }

    [Fact]
    public async Task AddLineAsync_RejectsWhenProductOutOfStock()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 0));

        var result = await service.AddLineAsync("widget", 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(cartStore.Cart.Lines);
    }

    [Fact]
    public async Task AddLineAsync_RejectsWhenAlreadyInCartQuantityWouldExceedStock()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 3));

        var first = await service.AddLineAsync("widget", 3, CancellationToken.None);
        var second = await service.AddLineAsync("widget", 1, CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(3, cartStore.Cart.Lines.Single().Quantity);
    }

    [Fact]
    public async Task UpdateLineQuantityAsync_RejectsWhenQuantityExceedsStock_AndLeavesQuantityUnchanged()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 3));
        await service.AddLineAsync("widget", 2, CancellationToken.None);

        var result = await service.UpdateLineQuantityAsync("widget", 10, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(2, cartStore.Cart.Lines.Single().Quantity);
    }

    [Fact]
    public async Task UpdateLineQuantityAsync_RemovesLineWhenQuantityIsZero()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 3));
        await service.AddLineAsync("widget", 2, CancellationToken.None);

        var result = await service.UpdateLineQuantityAsync("widget", 0, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(cartStore.Cart.Lines);
    }
}
