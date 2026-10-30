using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Helpers;

// One admin orders tab: its ?status= value, label, and what to say when it has no orders.
public sealed record AdminOrderTabLink(AdminOrderTab Tab, string Slug, string Label, string EmptyMessage);

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

    public static AdminOrderTabLink For(AdminOrderTab tab) => Links.First(l => l.Tab == tab);

    // Missing or unknown values fall back to the default tab rather than erroring.
    public static AdminOrderTabLink FromQuery(string? status) =>
        Links.FirstOrDefault(l => l.Slug.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? For(Default);
}
