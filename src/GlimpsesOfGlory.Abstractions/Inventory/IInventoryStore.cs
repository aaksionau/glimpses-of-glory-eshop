namespace GlimpsesOfGlory.Abstractions.Inventory;

public interface IInventoryStore
{
    // Atomically decrements stock only if enough is available, so concurrent
    // reservations against the same product can never oversell it. Returns
    // false (and leaves stock untouched) when the requested quantity exceeds
    // what's currently available.
    Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken);
}
