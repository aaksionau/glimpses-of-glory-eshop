using GlimpsesOfGlory.Abstractions.Inventory;
using GlimpsesOfGlory.Core.Products.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Products.Services;

public sealed class EfInventoryStore(AppDbContext dbContext) : IInventoryStore
{
    public async Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        var rowsAffected = await dbContext.Products
            .Where(p => p.Id == productId && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity), cancellationToken);

        return rowsAffected > 0;
    }
}
