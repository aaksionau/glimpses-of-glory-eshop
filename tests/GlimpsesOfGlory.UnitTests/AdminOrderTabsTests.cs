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
        Assert.Equal("/admin/orders", AdminOrderTabs.FromQuery(null).Href);
        Assert.Equal("/admin/orders?status=shipped", AdminOrderTabs.FromQuery("shipped").Href);
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

    private sealed class RecordingAdminOrderService : IAdminOrderService
    {
        public AdminOrderTab? RequestedTab { get; private set; }

        public Task<IReadOnlyList<AdminOrderSummary>> GetOrdersAsync(AdminOrderTab tab, CancellationToken cancellationToken)
        {
            RequestedTab = tab;
            return Task.FromResult<IReadOnlyList<AdminOrderSummary>>([]);
        }

        public Task<AdminOrderTabCounts> GetOrderTabCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AdminOrderTabCounts(1, 0, 2, 3));

        public Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
