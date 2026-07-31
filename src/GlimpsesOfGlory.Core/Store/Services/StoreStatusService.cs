using GlimpsesOfGlory.Abstractions.Store;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Store.Services;

public sealed class StoreStatusService(AppDbContext db) : IStoreStatusService
{
    public async Task<string?> GetCurrentMessageAsync(CancellationToken cancellationToken)
    {
        var status = await db.StoreStatuses.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);
        return status?.Message;
    }
}
