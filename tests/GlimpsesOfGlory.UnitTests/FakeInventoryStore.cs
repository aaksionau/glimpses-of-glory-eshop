using GlimpsesOfGlory.Abstractions.Inventory;

namespace GlimpsesOfGlory.UnitTests;

// In-memory IInventoryStore double with a real conditional-decrement contract (via a
// lock), so tests can exercise the "can't oversell under concurrency" guarantee without
// a real database.
public sealed class FakeInventoryStore(IDictionary<int, int> initialStock) : IInventoryStore
{
    private readonly Dictionary<int, int> _stock = new(initialStock);
    private readonly Lock _gate = new();

    public Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_stock.TryGetValue(productId, out var current) && current >= quantity)
            {
                _stock[productId] = current - quantity;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public int GetStock(int productId) => _stock[productId];
}
