using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Helpers;

// One admin orders tab: its ?status= value, label, and what to say when it has no orders.
public sealed record AdminOrderTabLink(AdminOrderTab Tab, string Slug, string Label, string EmptyMessage)
{
    // The default tab gets the bare /admin/orders URL so it has a single canonical address.
    public string Href => Tab == AdminOrderTabs.Default ? "/admin/orders" : $"/admin/orders?status={Slug}";
}

// The single table of admin order tabs, driving both the tab strip and the ?status= parsing.
public static class AdminOrderTabs
{
    public const AdminOrderTab Default = AdminOrderTab.NeedsAction;

    public static IReadOnlyList<AdminOrderTabLink> Links { get; } =
    [
        new(AdminOrderTab.NeedsAction, "needs-action", "Needs action", "Nothing needs action right now."),
        new(AdminOrderTab.Shipped, "shipped", "Shipped", "No shipped orders yet."),
        new(AdminOrderTab.Cancelled, "cancelled", "Cancelled", "No cancelled orders."),
        new(AdminOrderTab.All, "all", "All", "No orders yet."),
    ];

    // Missing or unknown values fall back to the default tab rather than erroring.
    public static AdminOrderTabLink FromQuery(string? status) =>
        Links.FirstOrDefault(l => l.Slug.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Links.First(l => l.Tab == Default);
}
