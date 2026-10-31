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
        var segments = Segments(path);
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

    // The aria-current value for a header link: "page" when the request is the link's own page,
    // "true" when it is somewhere inside that section (a product page under Shop), otherwise null.
    public static string? AriaCurrent(StorefrontSection link, string? path)
    {
        if (link == StorefrontSection.None || link != FromPath(path))
        {
            return null;
        }

        var segments = Segments(path);
        var isSectionPage = segments.Length <= 1
            || (segments.Length == 2 && segments[1].Equals("index", StringComparison.OrdinalIgnoreCase));
        return isSectionPage ? "page" : "true";
    }

    private static string[] Segments(string? path) =>
        (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
}
