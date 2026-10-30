using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Admin.Orders;

public class IndexModel(IAdminOrderService adminOrderService) : PageModel
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

    public AdminOrderListState State { get; private set; } = AdminOrderListState.FromQuery(null, null, false, null);

    public AdminOrderTabLink CurrentTab => AdminOrderTabs.For(State.Tab);

    public AdminOrderTabCounts Counts { get; private set; } = new(0, 0, 0, 0);

    public AdminOrderPage Orders { get; private set; } = new([], 1, AdminOrderListState.PageSize, 0);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        State = AdminOrderListState.FromQuery(Status, Search, Preorder, PageNumber, ToFulfill, UnpaidCash);
        Counts = await adminOrderService.GetOrderTabCountsAsync(State.Filter, cancellationToken);
        Orders = await adminOrderService.GetOrdersAsync(State.Tab, State.Filter, State.Page, AdminOrderListState.PageSize, cancellationToken);
        // The service falls back to the last page when the requested one is past the end.
        State = State with { Page = Orders.Page };
    }
}
