namespace GlimpsesOfGlory.Abstractions.Constants;

public static class OrderNotePolicy
{
    // Room for a few sentences ("customer called, collecting Friday"), not a document.
    public const int MaxLength = 2000;

    // The stored form of a note: line endings as "\n" (so the length matches what the browser's
    // maxlength counted), trimmed, and blank as null so "has a note" is a plain null check.
    public static string? Normalize(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        return note.ReplaceLineEndings("\n").Trim();
    }
}
