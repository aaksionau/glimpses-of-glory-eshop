namespace GlimpsesOfGlory.Abstractions.Services;

public interface IInventoryStore
{
    // Atomically decrements stock only if enough is available, so concurrent
    // reservations against the same product can never oversell it. Returns
    // false (and leaves stock untouched) when the requested quantity exceeds
    // what's currently available.
    Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken);

    // Preorder sales have no stock ceiling to guard against (aside from the per-line cap
    // enforced earlier in the flow), so this unconditionally increments the running counter.
    Task ReservePreorderAsync(int productId, int quantity, CancellationToken cancellationToken);
}
