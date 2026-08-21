namespace GlimpsesOfGlory.Web.Helpers;

public static class PreorderNoteFormatter
{
    public static string? Format(bool isPreorder, DateOnly? expectedAvailabilityDate)
    {
        if (!isPreorder)
        {
            return null;
        }

        return expectedAvailabilityDate is { } date
            ? $"Preorder — ships approx. {date:MMM d, yyyy}"
            : "Preorder";
    }
}
