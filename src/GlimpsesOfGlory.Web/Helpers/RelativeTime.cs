using System.Globalization;

namespace GlimpsesOfGlory.Web.Helpers;

public static class RelativeTime
{
    // Short "2 h ago" wording for recent moments; older ones fall back to a plain UTC date,
    // matching the UTC timestamp in the tooltip.
    public static string Format(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }
        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{(int)elapsed.TotalHours} h ago";
        }
        if (elapsed < TimeSpan.FromDays(7))
        {
            return $"{(int)elapsed.TotalDays} d ago";
        }
        return at.ToUniversalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    // Full timestamp for the tooltip behind a relative date.
    public static string FullTimestamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("MMMM d, yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
