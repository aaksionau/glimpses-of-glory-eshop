using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using Microsoft.AspNetCore.Http;

namespace GlimpsesOfGlory.Web.Helpers;

// Everything that decides what the admin orders list shows, as carried in its query string
// (?status=&q=&preorder=&pageNumber=), so a reload or a shared link lands on the same view.
public sealed record AdminOrderListState(AdminOrderTab Tab, string? Search, bool PreorderOnly, int Page)
{
    public const int PageSize = 25;

    public static AdminOrderListState FromQuery(string? status, string? search, bool preorderOnly, int? page) => new(
        AdminOrderTabs.FromQuery(status).Tab,
        string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        preorderOnly,
        page is > 1 ? page.Value : 1);

    public AdminOrderFilter Filter => new(Search, PreorderOnly);

    public bool IsFiltered => Search is not null || PreorderOnly;

    // Switching tabs keeps the search and filter but starts again from the first page.
    public string HrefForTab(AdminOrderTab tab) => (this with { Tab = tab, Page = 1 }).Href;

    public string HrefForPage(int page) => (this with { Page = page }).Href;

    public string ClearedHref => (this with { Search = null, PreorderOnly = false, Page = 1 }).Href;

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
            if (Page > 1)
            {
                query = query.Add("pageNumber", Page.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return "/admin/orders" + query.ToUriComponent();
        }
    }
}
