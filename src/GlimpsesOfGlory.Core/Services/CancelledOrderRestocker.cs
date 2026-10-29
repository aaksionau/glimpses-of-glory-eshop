using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// Reverses what OrderService reserved at checkout for each line of a cancelled order.
// Must run inside the same transaction as the status change so stock can't drift.
public static class CancelledOrderRestocker
{
    public static async Task RestockAsync(IInventoryStore inventoryStore, IEnumerable<OrderLine> lines, CancellationToken cancellationToken)
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
