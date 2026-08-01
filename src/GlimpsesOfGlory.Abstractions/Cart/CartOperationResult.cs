namespace GlimpsesOfGlory.Abstractions.Cart;

public sealed record CartOperationResult(bool Success, string? ErrorMessage)
{
    public static CartOperationResult Ok() => new(true, null);

    public static CartOperationResult Failed(string errorMessage) => new(false, errorMessage);
}
