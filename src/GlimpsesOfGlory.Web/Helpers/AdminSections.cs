namespace GlimpsesOfGlory.Web.Helpers;

public enum AdminSection
{
    None,
    Dashboard,
    Orders,
    AbandonedCheckouts,
    Products,
    Shipping,
}

// One sidebar entry: the section, the URL segment under /admin it owns, and its label.
public sealed record AdminSectionLink(AdminSection Section, string PathSegment, string Label)
{
    public string Href => PathSegment.Length == 0 ? "/admin" : $"/admin/{PathSegment}";
}

// The single table of admin sidebar sections. It drives both the sidebar links and the
// mapping from an /admin request path to the section to highlight, so adding a section
// means adding one entry here.
public static class AdminSections
{
    public static IReadOnlyList<AdminSectionLink> Links { get; } =
    [
        new(AdminSection.Dashboard, "", "Dashboard"),
        new(AdminSection.Orders, "orders", "Orders"),
        new(AdminSection.AbandonedCheckouts, "abandoned-checkouts", "Abandoned checkouts"),
        new(AdminSection.Products, "products", "Products"),
        new(AdminSection.Shipping, "shipping", "Shipping rates"),
    ];

    public static AdminSection FromPath(string? path)
    {
        var segments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !segments[0].Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return AdminSection.None;
        }

        if (segments.Length == 1 || segments[1].Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return AdminSection.Dashboard;
        }

        var link = Links.FirstOrDefault(l =>
            l.PathSegment.Length > 0 && l.PathSegment.Equals(segments[1], StringComparison.OrdinalIgnoreCase));
        return link?.Section ?? AdminSection.None;
    }
}
