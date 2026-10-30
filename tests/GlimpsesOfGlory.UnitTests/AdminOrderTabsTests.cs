using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Admin.Orders;

namespace GlimpsesOfGlory.UnitTests;

public class AdminOrderTabsTests
{
    [Theory]
    [InlineData(null, AdminOrderTab.NeedsAction)]
    [InlineData("", AdminOrderTab.NeedsAction)]
    [InlineData("needs-action", AdminOrderTab.NeedsAction)]
    [InlineData("shipped", AdminOrderTab.Shipped)]
    [InlineData("Shipped", AdminOrderTab.Shipped)]
    [InlineData("cancelled", AdminOrderTab.Cancelled)]
    [InlineData("all", AdminOrderTab.All)]
    [InlineData("bogus", AdminOrderTab.NeedsAction)]
    public void FromQuery_MapsStatusToTab(string? status, AdminOrderTab expected)
    {
        Assert.Equal(expected, AdminOrderTabs.FromQuery(status).Tab);
    }

    [Fact]
    public void Links_CoverEveryTabInOrder_DefaultingToNeedsAction()
    {
        Assert.Equal(
            [AdminOrderTab.NeedsAction, AdminOrderTab.Shipped, AdminOrderTab.Cancelled, AdminOrderTab.All],
            AdminOrderTabs.Links.Select(l => l.Tab));
        Assert.All(AdminOrderTabs.Links, l => Assert.False(string.IsNullOrWhiteSpace(l.EmptyMessage)));
    }

    [Fact]
    public async Task IndexPage_LoadsTheSelectedTabAndCounts()
    {
        var service = new RecordingAdminOrderService();
        var model = new IndexModel(service) { Status = "cancelled" };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(AdminOrderTab.Cancelled, service.RequestedTab);
        Assert.Equal(AdminOrderTab.Cancelled, model.CurrentTab.Tab);
        Assert.Equal(2, model.Counts.Cancelled);
    }

    [Fact]
    public async Task IndexPage_DefaultsToNeedsAction()
    {
        var service = new RecordingAdminOrderService();

        await new IndexModel(service).OnGetAsync(CancellationToken.None);

        Assert.Equal(AdminOrderTab.NeedsAction, service.RequestedTab);
    }

    [Fact]
    public async Task IndexPage_PassesSearchFilterAndPageThrough()
    {
        var service = new RecordingAdminOrderService();
        var model = new IndexModel(service) { Status = "all", Search = "  jane ", Preorder = true, PageNumber = 3 };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(AdminOrderTab.All, service.RequestedTab);
        Assert.Equal(new AdminOrderFilter("jane", PreorderOnly: true), service.RequestedFilter);
        Assert.Equal(new AdminOrderFilter("jane", PreorderOnly: true), service.CountedFilter);
        Assert.Equal(3, service.RequestedPage);
        Assert.Equal(AdminOrderListState.PageSize, service.RequestedPageSize);
    }

    [Fact]
    public async Task IndexPage_AdoptsThePageTheServiceActuallyReturned()
    {
        var service = new RecordingAdminOrderService { ReturnedPage = 2 };
        var model = new IndexModel(service) { PageNumber = 99 };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, model.State.Page);
    }

    [Theory]
    [InlineData(null, null, false, null, "/admin/orders")]
    [InlineData("needs-action", "", false, 1, "/admin/orders")]
    [InlineData("shipped", null, false, null, "/admin/orders?status=shipped")]
    [InlineData("all", " #42 ", true, 2, "/admin/orders?status=all&q=%2342&preorder=true&pageNumber=2")]
    [InlineData(null, "jane doe", false, 0, "/admin/orders?q=jane%20doe")]
    public void ListState_RoundTripsThroughTheQueryString(string? status, string? search, bool preorder, int? page, string expected)
    {
        Assert.Equal(expected, AdminOrderListState.FromQuery(status, search, preorder, page).Href);
    }

    [Fact]
    public async Task IndexPage_PassesTheToFulfillAndUnpaidCashFiltersThrough()
    {
        var service = new RecordingAdminOrderService();
        var model = new IndexModel(service) { ToFulfill = true, UnpaidCash = true };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(new AdminOrderFilter(ToFulfillOnly: true, UnpaidCashOnly: true), service.RequestedFilter);
        Assert.Equal(new AdminOrderFilter(ToFulfillOnly: true, UnpaidCashOnly: true), service.CountedFilter);
    }

    [Fact]
    public void ListState_CarriesTheToFulfillAndUnpaidCashFilters_AndClearDropsThem()
    {
        var state = AdminOrderListState.FromQuery("all", null, false, null, toFulfillOnly: true, unpaidCashOnly: true);

        Assert.Equal("/admin/orders?status=all&tofulfill=true&unpaidcash=true", state.Href);
        Assert.True(state.IsFiltered);
        Assert.Equal("/admin/orders?status=all", state.ClearedHref);
    }

    [Fact]
    public void ListState_TabLinksKeepTheSearchButResetThePage()
    {
        var state = AdminOrderListState.FromQuery("shipped", "jane", true, 4);

        Assert.Equal("/admin/orders?status=cancelled&q=jane&preorder=true", state.HrefForTab(AdminOrderTab.Cancelled));
        Assert.Equal("/admin/orders?q=jane&preorder=true", state.HrefForTab(AdminOrderTab.NeedsAction));
        Assert.Equal("/admin/orders?status=shipped&q=jane&preorder=true&pageNumber=5", state.HrefForPage(5));
        Assert.Equal("/admin/orders?status=shipped", state.ClearedHref);
        Assert.True(state.IsFiltered);
        Assert.False(AdminOrderListState.FromQuery("shipped", " ", false, null).IsFiltered);
    }

    // The page links and the PageModel binding must agree on the key, and it must not be "page",
    // which Razor Pages reserves for the page path (route values bind before the query string).
    [Fact]
    public void ListState_PageLinksUseTheKeyThePageModelBinds()
    {
        var binding = typeof(IndexModel).GetProperty(nameof(IndexModel.PageNumber))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.BindPropertyAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Mvc.BindPropertyAttribute>()
            .Single();

        Assert.NotEqual("page", binding.Name, StringComparer.OrdinalIgnoreCase);
        Assert.True(binding.SupportsGet);
        Assert.EndsWith($"?{binding.Name}=2", AdminOrderListState.FromQuery(null, null, false, 1).HrefForPage(2));
    }

    private sealed class RecordingAdminOrderService : IAdminOrderService
    {
        public AdminOrderTab? RequestedTab { get; private set; }

        public AdminOrderFilter? RequestedFilter { get; private set; }

        public AdminOrderFilter? CountedFilter { get; private set; }

        public int? RequestedPage { get; private set; }

        public int? RequestedPageSize { get; private set; }

        public int? ReturnedPage { get; init; }

        public Task<AdminOrderPage> GetOrdersAsync(AdminOrderTab tab, AdminOrderFilter filter, int page, int pageSize, CancellationToken cancellationToken)
        {
            RequestedTab = tab;
            RequestedFilter = filter;
            RequestedPage = page;
            RequestedPageSize = pageSize;
            return Task.FromResult(new AdminOrderPage([], ReturnedPage ?? page, pageSize, 0));
        }

        public Task<AdminOrderTabCounts> GetOrderTabCountsAsync(AdminOrderFilter filter, CancellationToken cancellationToken)
        {
            CountedFilter = filter;
            return Task.FromResult(new AdminOrderTabCounts(1, 0, 2, 3));
        }

        public Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
