namespace GlimpsesOfGlory.Web.Helpers;

public static class SeoText
{
    public static string Truncate(string text, int maxLength)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var truncated = normalized[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated.TrimEnd() + "…";
    }
}
