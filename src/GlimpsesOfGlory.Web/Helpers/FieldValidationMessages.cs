using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GlimpsesOfGlory.Web.Helpers;

// What FieldValidationAriaTagHelper and ValidationMessageIdTagHelper agree on: whether a field
// failed validation, and the id of its message.
internal static class FieldValidationMessages
{
    public static bool HasError(ViewContext viewContext, string fullName) =>
        viewContext.ViewData.ModelState.TryGetValue(fullName, out var entry) && entry.Errors.Count > 0;

    public static string MessageId(IHtmlGenerator generator, string fullName) =>
        TagBuilder.CreateSanitizedId(fullName, generator.IdAttributeDotReplacement) + "-error";
}
