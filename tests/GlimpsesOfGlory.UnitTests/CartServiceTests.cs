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

    // With no tiers given, defaults to a single flat $7.50 tier.
    private sealed class FakeShippingSettingsService(params ShippingTierView[] tiers) : IShippingSettingsService
    {
        private readonly IReadOnlyList<ShippingTierView> tiers = tiers.Length > 0 ? tiers : [new ShippingTierView(1, 1, 7.50m)];

        public Task<IReadOnlyList<ShippingTierView>> GetTiersAsync(CancellationToken cancellationToken)
            => Task.FromResult(tiers);

        public Task UpdateTiersAsync(IReadOnlyList<ShippingTierUpdate> tiers, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private static ProductDetail CreateProduct(int stockQuantity) =>
        new("widget", "Widget", "A widget", 10.00m, stockQuantity, false, null, []);

    private static CartService CreateService(FakeCartStore cartStore, ProductDetail? product) =>
        new(cartStore, new FakeProductCatalogService(product), new FakeShippingSettingsService());

    // $8 for 1-2 items, $5 from 3 items.
    private static CartService CreateTieredService(FakeCartStore cartStore, ProductDetail? product) =>
        new(cartStore, new FakeProductCatalogService(product), new FakeShippingSettingsService(
            new ShippingTierView(1, 1, 8.00m), new ShippingTierView(2, 3, 5.00m)));

    [Fact]
    public async Task GetSummaryAsync_ShippingCostFollowsQuantityChange()
    {
        var cartStore = new FakeCartStore();
        var service = CreateTieredService(cartStore, CreateProduct(stockQuantity: 10));
        await service.AddLineAsync("widget", 1, CancellationToken.None);

        await service.UpdateLineQuantityAsync("widget", 3, CancellationToken.None);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(5.00m, summary.ShippingCost);
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
