namespace GlimpsesOfGlory.UnitTests;

public class InventoryStoreTests
{
    [Fact]
    public async Task TryReserveStockAsync_ReservesStock_WhenEnoughIsAvailable()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 10 });

        var reserved = await store.TryReserveStockAsync(1, 3, CancellationToken.None);

        Assert.True(reserved);
        Assert.Equal(7, store.GetStock(1));
    }

    [Fact]
    public async Task TryReserveStockAsync_RejectsReservation_WhenStockIsInsufficient()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 2 });

        var reserved = await store.TryReserveStockAsync(1, 5, CancellationToken.None);

        Assert.False(reserved);
        Assert.Equal(2, store.GetStock(1));
    }

    [Fact]
    public async Task TryReserveStockAsync_AllowsOnlyOneWinner_WhenTwoConcurrentReservationsRaceForLastUnit()
    {
        var store = new FakeInventoryStore(new Dictionary<int, int> { [1] = 1 });

        var results = await Task.WhenAll(
            store.TryReserveStockAsync(1, 1, CancellationToken.None),
            store.TryReserveStockAsync(1, 1, CancellationToken.None));

        Assert.Single(results, true);
        Assert.Single(results, false);
        Assert.Equal(0, store.GetStock(1));
    }
}
