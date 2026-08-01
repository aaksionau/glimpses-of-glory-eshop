using GlimpsesOfGlory.Abstractions.Products;

namespace GlimpsesOfGlory.Abstractions.Cart;

public sealed record CartOperationResult(bool Success, string? ErrorMessage, ProductDetail? Product = null)
{
    public static CartOperationResult Ok() => new(true, null);

    public static CartOperationResult Failed(string errorMessage, ProductDetail? product = null) => new(false, errorMessage, product);
}
