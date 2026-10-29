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

    // Admin order detail: the date the preorder line is expected to become available.
    public static string ExpectedAvailability(DateOnly? expectedAvailabilityDate) =>
        expectedAvailabilityDate is { } date
            ? $"Expected available {date:MMM d, yyyy}"
            : "No expected availability date";
}
