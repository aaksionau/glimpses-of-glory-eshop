using System.Text.Json;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Formatting;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GlimpsesOfGlory.Web.Helpers;

// Carries checkout's stock failures across the redirect to the cart, so the cart can flag each
// short line. With no line-level detail it falls back to the generic ItemsUnavailable banner.
public static class CartLineErrorsTempData
{
    // The cart page's [TempData] Error property, which shows the banner.
    private const string ErrorKey = nameof(Pages.Cart.IndexModel.Error);

    private const string LineErrorsKey = "CartLineErrors";

    public static void ReportUnavailable(ITempDataDictionary tempData, IReadOnlyList<UnavailableCartLine> unavailableLines)
    {
        if (unavailableLines.Count == 0)
        {
            tempData[ErrorKey] = CheckoutMessages.ItemsUnavailable;
            return;
        }

        var errors = unavailableLines
            .Select(l => new CartLineError(l.ProductSlug, CheckoutMessages.LineUnavailable(l.AvailableQuantity)))
            .ToList();
        tempData[LineErrorsKey] = JsonSerializer.Serialize(errors);
    }

    public static IReadOnlyList<CartLineError> Read(ITempDataDictionary tempData) =>
        tempData[LineErrorsKey] is string json
            ? JsonSerializer.Deserialize<List<CartLineError>>(json) ?? []
            : [];
}
