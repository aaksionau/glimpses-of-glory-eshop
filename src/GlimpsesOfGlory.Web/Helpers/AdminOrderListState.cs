using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using Microsoft.AspNetCore.Http;

namespace GlimpsesOfGlory.Web.Helpers;

// Everything that decides what the admin orders list shows, as carried in its query string
// (?status=&q=&preorder=&tofulfill=&unpaidcash=&pageNumber=), so a reload or a shared link lands
// on the same view.
public sealed record AdminOrderListState(AdminOrderTab Tab, string? Search, bool PreorderOnly, int Page, bool ToFulfillOnly = false, bool UnpaidCashOnly = false)
{
    public const int PageSize = 25;

    public static AdminOrderListState FromQuery(string? status, string? search, bool preorderOnly, int? page, bool toFulfillOnly = false, bool unpaidCashOnly = false) => new(
        AdminOrderTabs.FromQuery(status).Tab,
        string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        preorderOnly,
        page is > 1 ? page.Value : 1,
        toFulfillOnly,
        unpaidCashOnly);

    public AdminOrderFilter Filter => new(Search, PreorderOnly, ToFulfillOnly, UnpaidCashOnly);

    public bool IsFiltered => Search is not null || PreorderOnly || ToFulfillOnly || UnpaidCashOnly;

    // Switching tabs keeps the search and filter but starts again from the first page.
    public string HrefForTab(AdminOrderTab tab) => (this with { Tab = tab, Page = 1 }).Href;

    public string HrefForPage(int page) => (this with { Page = page }).Href;

    public string ClearedHref => (this with { Search = null, PreorderOnly = false, ToFulfillOnly = false, UnpaidCashOnly = false, Page = 1 }).Href;

    // Defaults are left out so the plain list keeps its bare /admin/orders address.
    public string Href
    {
        get
        {
            var query = QueryString.Empty;
            if (Tab != AdminOrderTabs.Default)
            {
                query = query.Add("status", AdminOrderTabs.For(Tab).Slug);
            }
            if (Search is not null)
            {
                query = query.Add("q", Search);
            }
            if (PreorderOnly)
            {
                query = query.Add("preorder", "true");
            }
            if (ToFulfillOnly)
            {
                query = query.Add("tofulfill", "true");
            }
            if (UnpaidCashOnly)
            {
                query = query.Add("unpaidcash", "true");
            }
            if (Page > 1)
            {
                query = query.Add("pageNumber", Page.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return "/admin/orders" + query.ToUriComponent();
        }
    }
}
