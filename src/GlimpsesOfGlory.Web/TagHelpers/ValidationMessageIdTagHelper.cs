using GlimpsesOfGlory.Web.Formatting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace GlimpsesOfGlory.Web.TagHelpers;

// Gives a server-side validation message span the id "<field id>-error" so the invalid field's
// aria-describedby (see FieldValidationAriaTagHelper) can point at it. wwwroot/js/form-validation.js
// gives the same span the same id when it shows a message there.
[HtmlTargetElement("span", Attributes = ForAttributeName)]
public sealed class ValidationMessageIdTagHelper(IHtmlGenerator generator) : TagHelper
{
    private const string ForAttributeName = "asp-validation-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var fullName = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(For.Name);
        if (FieldValidationMessages.HasError(ViewContext, fullName))
        {
            output.Attributes.SetAttribute("id", FieldValidationMessages.MessageId(generator, fullName));
        }
    }
}
