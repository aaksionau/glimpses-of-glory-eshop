namespace GlimpsesOfGlory.Abstractions.Constants;

public static class PreorderPolicy
{
    // Fixed abuse guard, not a configurable business setting - preorder stock has no
    // real ceiling, but an unbounded quantity per line would be exploitable.
    public const int MaxQuantityPerLine = 20;
}
