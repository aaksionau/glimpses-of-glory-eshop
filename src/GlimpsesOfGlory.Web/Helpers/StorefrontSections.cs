namespace GlimpsesOfGlory.Web.Helpers;

public enum StorefrontSection
{
    None,
    Home,
    Shop,
    Cart,
}

// Maps a request path to the storefront header link to highlight as the current section.
// Product pages (/products/{slug}) belong to Shop; checkout and other pages highlight nothing.
public static class StorefrontSections
{
    public static StorefrontSection FromPath(string? path)
    {
        var segments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return StorefrontSection.Home;
        }

        return segments[0].ToLowerInvariant() switch
        {
            "index" when segments.Length == 1 => StorefrontSection.Home,
            "products" => StorefrontSection.Shop,
            "cart" => StorefrontSection.Cart,
            _ => StorefrontSection.None,
        };
    }
}
