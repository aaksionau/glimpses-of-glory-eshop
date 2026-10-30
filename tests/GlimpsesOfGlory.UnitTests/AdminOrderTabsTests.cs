using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Admin.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
        var model = new IndexModel(service, TimeProvider.System) { Status = "cancelled" };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(AdminOrderTab.Cancelled, service.RequestedTab);
        Assert.Equal(AdminOrderTab.Cancelled, model.CurrentTab.Tab);
        Assert.Equal(2, model.Counts.Cancelled);
    }

    [Fact]
    public async Task IndexPage_DefaultsToNeedsAction()
    {
        var service = new RecordingAdminOrderService();

        await new IndexModel(service, TimeProvider.System).OnGetAsync(CancellationToken.None);

        Assert.Equal(AdminOrderTab.NeedsAction, service.RequestedTab);
    }

    [Fact]
    public async Task IndexPage_PassesSearchFilterAndPageThrough()
    {
        var service = new RecordingAdminOrderService();
        var model = new IndexModel(service, TimeProvider.System) { Status = "all", Search = "  jane ", Preorder = true, PageNumber = 3 };

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
        var model = new IndexModel(service, TimeProvider.System) { PageNumber = 99 };

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
        var model = new IndexModel(service, TimeProvider.System) { ToFulfill = true, UnpaidCash = true };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(new AdminOrderFilter(ToFulfillOnly: true, UnpaidCashOnly: true), service.RequestedFilter);
        Assert.Equal(new AdminOrderFilter(ToFulfillOnly: true, UnpaidCashOnly: true), service.CountedFilter);
    }

    [Fact]
    public async Task IndexPage_DefaultsTheExportRangeToTheCurrentMonthSoFar()
    {
        var model = new IndexModel(new RecordingAdminOrderService(), new FixedTimeProvider(new DateTimeOffset(2026, 10, 30, 23, 0, 0, TimeSpan.Zero)));

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 1), model.ExportFrom);
        Assert.Equal(new DateOnly(2026, 10, 30), model.ExportTo);
    }

    [Fact]
    public async Task IndexPage_TakesNowForRelativeTimesFromTheInjectedClock()
    {
        var now = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        var model = new IndexModel(new RecordingAdminOrderService(), new FixedTimeProvider(now));

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(now, model.Now);
    }

    [Fact]
    public async Task IndexPage_Export_StreamsTheRangeAsACsvAttachment()
    {
        var service = new RecordingAdminOrderService
        {
            ExportRows = [new AdminOrderExportRow(7, new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero), "Jane", "jane@example.com", FulfillmentMethod.Shipping, OrderStatus.New, PaymentMethod.Card, null, 10m, 5m)],
        };
        var (model, body) = ExportPage(service, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        var result = await model.OnGetExportAsync(CancellationToken.None);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), service.ExportedRange);
        Assert.Equal(OrderCsvExport.ContentType, model.Response.ContentType);
        Assert.Equal("attachment; filename=orders-2026-10-01-to-2026-10-31.csv", model.Response.Headers.ContentDisposition.ToString());
        var csv = System.Text.Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("\r\n7,2026-10-02 09:30,Jane,jane@example.com,Shipping,New,Card,,10.00,5.00,15.00\r\n", csv);
    }

    [Theory]
    [InlineData(null, "2026-10-31")]
    [InlineData("2026-10-01", null)]
    [InlineData("2026-10-31", "2026-10-01")]
    public async Task IndexPage_Export_WithAMissingOrBackwardsRange_ShowsAnErrorInsteadOfExporting(string? from, string? to)
    {
        var service = new RecordingAdminOrderService();
        var (model, body) = ExportPage(service, from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to));

        var result = await model.OnGetExportAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.ExportError);
        Assert.Null(service.ExportedRange);
        Assert.Equal(0, body.Length);
        Assert.NotNull(service.RequestedTab);
    }

    private static (IndexModel Model, MemoryStream Body) ExportPage(RecordingAdminOrderService service, DateOnly? from, DateOnly? to)
    {
        var body = new MemoryStream();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = body;
        var model = new IndexModel(service, TimeProvider.System)
        {
            ExportFrom = from,
            ExportTo = to,
            PageContext = new PageContext { HttpContext = httpContext },
        };
        return (model, body);
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
            .GetCustomAttributes(typeof(BindPropertyAttribute), inherit: false)
            .Cast<BindPropertyAttribute>()
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

        public (DateOnly From, DateOnly To)? ExportedRange { get; private set; }

        public List<AdminOrderExportRow> ExportRows { get; init; } = [];

        public IAsyncEnumerable<AdminOrderExportRow> StreamOrdersForExportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            ExportedRange = (from, to);
            return ExportRows.ToAsyncEnumerable();
        }

        public Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, ShippingCarrier carrier, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> UpdateInternalNoteAsync(int id, string? note, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EmailResendResult> ResendConfirmationAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<EmailResendResult> ResendShippedEmailAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
