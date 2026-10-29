using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

public class CancelledOrderRestockerTests
{
    [Fact]
    public async Task RestockAsync_ReturnsInStockLineQuantityToStock()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 10 });
        await store.TryReserveStockAsync(1, 3, CancellationToken.None);

        await CancelledOrderRestocker.RestockAsync(store, [Line(1, 3, isPreorder: false)], CancellationToken.None);

        Assert.Equal(10, store.GetStock(1));
    }

    [Fact]
    public async Task RestockAsync_ReleasesPreorderLine_WhileProductIsStillOnPreorder()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 0 });
        await store.ReservePreorderAsync(1, 4, CancellationToken.None);
        await store.ReservePreorderAsync(1, 2, CancellationToken.None);

        await CancelledOrderRestocker.RestockAsync(store, [Line(1, 4, isPreorder: true)], CancellationToken.None);

        Assert.Equal(2, store.GetPreordered(1));
        Assert.Equal(0, store.GetStock(1));
    }

    [Fact]
    public async Task RestockAsync_ReturnsPreorderLineToStock_OnceStockWasReceived()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 0 });
        await store.ReservePreorderAsync(1, 4, CancellationToken.None);
        store.ReceivePreorderStock(1, quantityReceived: 10);

        await CancelledOrderRestocker.RestockAsync(store, [Line(1, 4, isPreorder: true)], CancellationToken.None);

        // The 4 units the reconciler set aside for this buyer are free again.
        Assert.Equal(10, store.GetStock(1));
        Assert.Equal(0, store.GetPreordered(1));
    }

    [Fact]
    public async Task RestockAsync_RestoresEachLineOfAMixedOrder()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 5, [2] = 0 });
        await store.TryReserveStockAsync(1, 2, CancellationToken.None);
        await store.ReservePreorderAsync(2, 3, CancellationToken.None);

        await CancelledOrderRestocker.RestockAsync(store, [Line(1, 2, isPreorder: false), Line(2, 3, isPreorder: true)], CancellationToken.None);

        Assert.Equal(5, store.GetStock(1));
        Assert.Equal(0, store.GetPreordered(2));
    }

    private static OrderLine Line(int productId, int quantity, bool isPreorder) => new()
    {
        ProductId = productId,
        ProductName = $"Product {productId}",
        UnitPrice = 10m,
        Quantity = quantity,
        IsPreorder = isPreorder,
    };
}
