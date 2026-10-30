using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace GlimpsesOfGlory.Web.Helpers;

// Marks a form control that failed server-side validation as aria-invalid and points its
// aria-describedby at the message that ValidationMessageIdTagHelper renders for it, so assistive
// tech reads the error out with the field. wwwroot/js/form-validation.js uses the same id
// ("<field id>-error") for the messages it shows, so the link survives the field being re-checked
// in the browser.
[HtmlTargetElement("input", Attributes = ForAttributeName)]
[HtmlTargetElement("select", Attributes = ForAttributeName)]
[HtmlTargetElement("textarea", Attributes = ForAttributeName)]
public sealed class FieldValidationAriaTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // A hidden input is never shown or focused, so there's nothing to mark (e.g. admin's Tiers[i].Id).
        if (context.AllAttributes.TryGetAttribute("type", out var type)
            && string.Equals(type.Value?.ToString(), "hidden", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fullName = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (!FieldValidationMessages.HasError(ViewContext, fullName))
        {
            return;
        }

        var messageId = FieldValidationMessages.MessageId(generator, fullName);
        var describedBy = output.Attributes.TryGetAttribute("aria-describedby", out var existing)
            ? $"{existing.Value} {messageId}"
            : messageId;

        output.Attributes.SetAttribute("aria-invalid", "true");
        output.Attributes.SetAttribute("aria-describedby", describedBy);
    }
}
