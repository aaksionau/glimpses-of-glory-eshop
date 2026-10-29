using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

// In-memory IInventoryStore double with a real conditional-decrement contract (via a
// lock), so tests can exercise the "can't oversell under concurrency" guarantee without
// a real database.
public sealed class FakeInventoryStore(IDictionary<int, int> initialStock) : IInventoryStore
{
    private readonly Dictionary<int, int> _stock = new(initialStock);
    private readonly Dictionary<int, int> _preordered = [];
    private readonly HashSet<int> _received = [];
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

    public Task ReservePreorderAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _preordered[productId] = _preordered.GetValueOrDefault(productId) + quantity;
            return Task.CompletedTask;
        }
    }

    public Task RestockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            AddStock(productId, quantity);
            return Task.CompletedTask;
        }
    }

    public Task ReleasePreorderAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_received.Contains(productId))
            {
                AddStock(productId, quantity);
            }
            else
            {
                _preordered[productId] = Math.Max(0, _preordered.GetValueOrDefault(productId) - quantity);
            }
            return Task.CompletedTask;
        }
    }

    // Mirrors AdminProductService.ReceiveStockAsync: the product leaves preorder and its
    // stock becomes what was received minus what preorder buyers are already owed.
    public void ReceivePreorderStock(int productId, int quantityReceived)
    {
        lock (_gate)
        {
            _stock[productId] = PreorderStockReconciler.Reconcile(quantityReceived, _preordered.GetValueOrDefault(productId));
            _preordered[productId] = 0;
            _received.Add(productId);
        }
    }

    // Caller must hold _gate.
    private void AddStock(int productId, int quantity) =>
        _stock[productId] = _stock.GetValueOrDefault(productId) + quantity;

    public int GetStock(int productId) => _stock[productId];

    public int GetPreordered(int productId) => _preordered.GetValueOrDefault(productId);
}
