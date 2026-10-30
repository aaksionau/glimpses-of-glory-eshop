using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// The one place that decides whether an order line touches the preorder counter or shelf
// stock. Reserve (checkout) and release (cancel) sit side by side so they can't drift apart.
public sealed class OrderLineInventory(IInventoryStore inventoryStore)
{
    // Must run inside a transaction - on false the caller rolls back so the lines
    // already reserved don't stand.
    public async Task<bool> TryReserveAsync(IEnumerable<ICheckoutLine> lines, CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (line.IsPreorder)
            {
                await inventoryStore.ReservePreorderAsync(line.ProductId, line.Quantity, cancellationToken);
                continue;
            }

            if (!await inventoryStore.TryReserveStockAsync(line.ProductId, line.Quantity, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    // Reverses TryReserveAsync for each line of a cancelled order. Must run inside the same
    // transaction as the status change so stock can't drift.
    public async Task ReleaseAsync(IEnumerable<ICheckoutLine> lines, CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (line.IsPreorder)
            {
                await inventoryStore.ReleasePreorderAsync(line.ProductId, line.Quantity, cancellationToken);
            }
            else
            {
                await inventoryStore.RestockAsync(line.ProductId, line.Quantity, cancellationToken);
            }
        }
    }
}
