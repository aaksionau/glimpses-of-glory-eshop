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

    public async Task RestockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        await dbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity), cancellationToken);
    }

    public async Task ReleasePreorderAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        // Only one of these matches, depending on whether the preorder stock has been received yet.
        await dbContext.Products
            .Where(p => p.Id == productId && p.IsPreorder)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PreorderedQuantity, p => Math.Max(0, p.PreorderedQuantity - quantity)), cancellationToken);

        await dbContext.Products
            .Where(p => p.Id == productId && !p.IsPreorder)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity), cancellationToken);
    }
}
