using System.Text.RegularExpressions;

namespace GlimpsesOfGlory.Core.Products.Services;

internal static partial class ProductSlugGenerator
{
    public static string Generate(string name, IReadOnlySet<string> existingSlugs)
    {
        var baseSlug = Slugify(name);
        if (!existingSlugs.Contains(baseSlug))
        {
            return baseSlug;
        }

        var suffix = 2;
        string candidate;
        do
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        } while (existingSlugs.Contains(candidate));

        return candidate;
    }

    private static string Slugify(string name)
    {
        var lowered = name.Trim().ToLowerInvariant();
        var withHyphens = NonAlphanumericRegex().Replace(lowered, "-");
        var collapsed = MultipleHyphensRegex().Replace(withHyphens, "-").Trim('-');
        return collapsed.Length == 0 ? "product" : collapsed;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
