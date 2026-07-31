using GlimpsesOfGlory.Abstractions.Store;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages;

public class IndexModel(IStoreStatusService storeStatusService) : PageModel
{
    public string StatusMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        StatusMessage = await storeStatusService.GetCurrentMessageAsync(cancellationToken) ?? "No status available.";
    }
}
