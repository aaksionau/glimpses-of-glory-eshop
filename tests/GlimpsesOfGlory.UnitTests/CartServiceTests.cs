using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Products;
using GlimpsesOfGlory.Abstractions.Shipping;
using GlimpsesOfGlory.Core.Cart.Services;

namespace GlimpsesOfGlory.UnitTests;

public class CartServiceTests
{
    private sealed class FakeCartStore : ICartStore
    {
        public Cart Cart { get; } = new();

        public Task<Cart> GetCartAsync(CancellationToken cancellationToken) => Task.FromResult(Cart);

        public Task SaveCartAsync(Cart cart, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeProductCatalogService(ProductDetail? product) : IProductCatalogService
    {
        public Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProductSummary>>([]);

        public Task<ProductDetail?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken)
            => Task.FromResult(product);
    }

    private sealed class FakeShippingSettingsService : IShippingSettingsService
    {
        public Task<IReadOnlyList<ShippingTierView>> GetTiersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ShippingTierView>>([new ShippingTierView(1, 1, 0m)]);

        public Task UpdateTiersAsync(IReadOnlyList<ShippingTierUpdate> tiers, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private static ProductDetail CreateProduct(int stockQuantity) =>
        new("widget", "Widget", "A widget", 10.00m, stockQuantity, []);

    private static CartService CreateService(FakeCartStore cartStore, ProductDetail? product) =>
        new(cartStore, new FakeProductCatalogService(product), new FakeShippingSettingsService());

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
    public async Task AddLineAsync_RejectsWhenQuantityExceedsStock()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 3));

        var result = await service.AddLineAsync("widget", 5, CancellationToken.None);

        Assert.False(result.Success);
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
    public async Task AddLineAsync_SucceedsWhenWithinStock()
    {
        var cartStore = new FakeCartStore();
        var service = CreateService(cartStore, CreateProduct(stockQuantity: 5));

        var result = await service.AddLineAsync("widget", 3, CancellationToken.None);

        Assert.True(result.Success);
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
