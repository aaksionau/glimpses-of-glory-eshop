using GlimpsesOfGlory.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Web.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public string StatusMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var status = await db.StoreStatuses.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);
        StatusMessage = status?.Message ?? "No status available.";
    }
}
