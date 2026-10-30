using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace GlimpsesOfGlory.Web.Helpers;

// Wraps a server-side validation message in <span id="<field id>-error"> so the invalid field's
// aria-describedby (see FieldValidationAriaTagHelper) can point at it. The id goes on an inner span
// rather than the container because jQuery Validate unobtrusive replaces the container's contents
// with its own message under that same id when the field is re-checked in the browser.
[HtmlTargetElement("span", Attributes = ForAttributeName)]
public sealed class ValidationMessageIdTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-validation-for";

    // Run after the built-in ValidationMessageTagHelper has filled in the message.
    public override int Order => 1;

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var fullName = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (!FieldValidationMessages.HasError(ViewContext, fullName) || output.Content.IsEmptyOrWhiteSpace)
        {
            return;
        }

        var message = new TagBuilder("span");
        message.Attributes["id"] = FieldValidationMessages.MessageId(generator, fullName);
        message.InnerHtml.SetHtmlContent(new HtmlString(output.Content.GetContent()));
        output.Content.SetHtmlContent(message);
    }
}
