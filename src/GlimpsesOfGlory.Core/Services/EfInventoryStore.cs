using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

public sealed class EfInventoryStore(AppDbContext dbContext) : IInventoryStore
{
    public async Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        var rowsAffected = await dbContext.Products
            .Where(p => p.Id == productId && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity), cancellationToken);

        return rowsAffected > 0;
    }

    public async Task ReservePreorderAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        await dbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PreorderedQuantity, p => p.PreorderedQuantity + quantity), cancellationToken);
    }
}
