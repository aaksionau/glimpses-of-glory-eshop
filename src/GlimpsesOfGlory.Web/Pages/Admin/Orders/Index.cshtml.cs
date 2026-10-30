using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

public class IndexModel(IAdminOrderService adminOrderService, TimeProvider timeProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Preorder { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ToFulfill { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool UnpaidCash { get; set; }

    // Not "page": Razor Pages reserves that route value for the page path, and route values are
    // read before the query string.
    [BindProperty(Name = "pageNumber", SupportsGet = true)]
    public int? PageNumber { get; set; }

    // The CSV export's inclusive UTC date range; defaults to the current month so far.
    [BindProperty(Name = "from", SupportsGet = true)]
    public DateOnly? ExportFrom { get; set; }

    [BindProperty(Name = "to", SupportsGet = true)]
    public DateOnly? ExportTo { get; set; }

    public string? ExportError { get; private set; }

    public AdminOrderListState State { get; private set; } = AdminOrderListState.FromQuery(null, null, false, null);

    public AdminOrderTabLink CurrentTab => AdminOrderTabs.For(State.Tab);

    public AdminOrderTabCounts Counts { get; private set; } = new(0, 0, 0, 0);

    public AdminOrderPage Orders { get; private set; } = new([], 1, AdminOrderListState.PageSize, 0);

    public Task OnGetAsync(CancellationToken cancellationToken) => LoadListAsync(cancellationToken);

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        if (ExportFrom is not { } from || ExportTo is not { } to)
        {
            ExportError = "Choose both a from and a to date to export.";
        }
        else if (to < from)
        {
            ExportError = "The to date can't be before the from date.";
        }
        else
        {
            Response.ContentType = OrderCsvExport.ContentType;
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = OrderCsvExport.FileName(from, to),
            }.ToString();
            await OrderCsvExport.WriteAsync(adminOrderService.StreamOrdersForExport(from, to), Response.Body, cancellationToken);
            return new EmptyResult();
        }

        await LoadListAsync(cancellationToken);
        return Page();
    }

    private async Task LoadListAsync(CancellationToken cancellationToken)
    {
        var (defaultFrom, defaultTo) = OrderCsvExport.DefaultRange(timeProvider.GetUtcNow());
        ExportFrom ??= defaultFrom;
        ExportTo ??= defaultTo;

        State = AdminOrderListState.FromQuery(Status, Search, Preorder, PageNumber, ToFulfill, UnpaidCash);
        Counts = await adminOrderService.GetOrderTabCountsAsync(State.Filter, cancellationToken);
        Orders = await adminOrderService.GetOrdersAsync(State.Tab, State.Filter, State.Page, AdminOrderListState.PageSize, cancellationToken);
        // The service falls back to the last page when the requested one is past the end.
        State = State with { Page = Orders.Page };
    }
}
