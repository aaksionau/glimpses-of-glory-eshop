namespace GlimpsesOfGlory.Web.Helpers;

public enum AdminSection
{
    None,
    Dashboard,
    Orders,
    Products,
    Shipping,
}

// Maps an /admin request path to the sidebar section it belongs to, so the admin
// layout can highlight the current section without every page declaring it.
public static class AdminSections
{
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

        return segments[1].ToLowerInvariant() switch
        {
            "orders" => AdminSection.Orders,
            "products" => AdminSection.Products,
            "shipping" => AdminSection.Shipping,
            _ => AdminSection.None,
        };
    }
}
