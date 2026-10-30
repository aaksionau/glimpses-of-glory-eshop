using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutDetailsFormTests
{
    // Stands in for Index.cshtml as the view hosting the <partial> tag; never rendered itself.
    private sealed class HostPageView : IView
    {
        public string Path => "/Pages/Checkout/Index.cshtml";

        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    // The standard autofill tokens browsers and password managers use to fill the
    // contact and address fields in one go, keyed by CheckoutAddress property.
    public static TheoryData<string, string> AutocompleteTokens => new()
    {
        { "Email", "email" },
        { "FullName", "name" },
        { "AddressLine1", "address-line1" },
        { "AddressLine2", "address-line2" },
        { "City", "address-level2" },
        { "State", "address-level1" },
        { "PostalCode", "postal-code" },
        { "Country", "country-name" },
    };

    [Theory]
    [MemberData(nameof(AutocompleteTokens))]
    public async Task DetailsField_HasAutocompleteToken(string fieldName, string token)
    {
        var field = FindField(await RenderAsync(), fieldName);

        Assert.Contains($"autocomplete=\"{token}\"", field);
    }

    [Fact]
    public async Task EveryDetailsField_HasAnAutocompleteToken()
    {
        var fields = Fields(await RenderAsync()).ToList();

        Assert.Equal(AutocompleteTokens.Count, fields.Count);
        Assert.All(fields, f => Assert.Contains("autocomplete=\"", f));
    }

    [Fact]
    public async Task Country_IsASelectOfSupportedCountries_NotTyped()
    {
        var html = await RenderAsync();

        Assert.StartsWith("<select", FindField(html, "Country"));
        Assert.Equal(ShippingCountryPolicy.SupportedCountries, Options(html).Select(o => o.Value));
    }

    [Fact]
    public async Task Country_DefaultsToUnitedStates_WhenNoCountrySet()
    {
        var options = Options(await RenderAsync(new CheckoutAddress { Country = null }));

        var selected = Assert.Single(options, o => o.Selected);
        Assert.Equal("United States", selected.Value);
    }

    public static TheoryData<string> SupportedCountries => new(ShippingCountryPolicy.SupportedCountries);

    // A re-render after a failed post keeps the shopper's choice.
    [Theory]
    [MemberData(nameof(SupportedCountries))]
    public async Task Country_KeepsPostedSupportedCountry_OnRerender(string country)
    {
        var options = Options(await RenderAsync(new CheckoutAddress { Country = country }));

        var selected = Assert.Single(options, o => o.Selected);
        Assert.Equal(country, selected.Value);
    }

    // E.g. a free-text "USA" left in the checkout session from before the select existed.
    [Fact]
    public async Task Country_FallsBackToDefault_WhenCountryIsUnsupported()
    {
        var options = Options(await RenderAsync(new CheckoutAddress { Country = "USA" }));

        var selected = Assert.Single(options, o => o.Selected);
        Assert.Equal(ShippingCountryPolicy.DefaultCountry, selected.Value);
    }

    // The checkout page renders the fields with <partial for="Address">, so they must post
    // under the "Address." prefix that IndexModel's [BindProperty] Address binds from.
    [Theory]
    [MemberData(nameof(AutocompleteTokens))]
    public async Task DetailsField_OnCheckoutPage_KeepsAddressPrefix(string fieldName, string token)
    {
        var field = FindField(await RenderOnCheckoutPageAsync(new ModelStateDictionary()), $"Address.{fieldName}");

        Assert.Contains($"id=\"Address_{fieldName}\"", field);
        Assert.Contains($"autocomplete=\"{token}\"", field);
    }

    [Fact]
    public async Task ValidationMessage_OnCheckoutPage_ShowsErrorForPrefixedField()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Address.Email", "Enter your email.");

        var html = await RenderOnCheckoutPageAsync(modelState);

        Assert.Matches("<span [^>]*data-valmsg-for=\"Address.Email\"[^>]*><span id=\"Address_Email-error\">Enter your email.</span></span>", html);
    }

    // The id is the one jQuery Validate gives its own message when it re-checks the field, so
    // assistive tech stays pointed at the current message either way.
    [Theory]
    [MemberData(nameof(AutocompleteTokens))]
    public async Task InvalidField_OnCheckoutPage_IsLinkedToItsMessage(string fieldName, string _)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError($"Address.{fieldName}", "Something is wrong.");

        var html = await RenderOnCheckoutPageAsync(modelState);
        var field = FindField(html, $"Address.{fieldName}");

        Assert.Contains("aria-invalid=\"true\"", field);
        Assert.Contains($"aria-describedby=\"Address_{fieldName}-error\"", field);
        Assert.Matches($"<span [^>]*data-valmsg-for=\"Address.{fieldName}\"[^>]*><span id=\"Address_{fieldName}-error\">Something is wrong.</span></span>", html);
    }

    [Fact]
    public async Task ValidFields_OnCheckoutPage_AreNotMarkedInvalid()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Address.City", "City is required.");

        var html = await RenderOnCheckoutPageAsync(modelState);

        Assert.All(
            Fields(html).Where(f => !f.Contains("name=\"Address.City\"")),
            f => Assert.DoesNotContain("aria-", f));
        Assert.Equal("id=\"Address_City-error\"", Assert.Single(Regex.Matches(html, "id=\"[^\"]*-error\"")).Value);
    }

    // Each field has a message span for jQuery Validate unobtrusive to show its error in.
    [Theory]
    [MemberData(nameof(AutocompleteTokens))]
    public async Task DetailsField_HasAMessageSpan(string fieldName, string _)
    {
        var html = await RenderOnCheckoutPageAsync(new ModelStateDictionary());

        Assert.Contains($"data-valmsg-for=\"Address.{fieldName}\"", html);
    }

    // The shipping-only fields aren't [Required] (they're checked in CheckoutAddress.Validate),
    // so the browser catches them by their required attribute and shows the server's message.
    public static TheoryData<string> ShippingRequiredFields => new(CheckoutAddress.ShippingRequiredMessages.Keys);

    [Theory]
    [MemberData(nameof(ShippingRequiredFields))]
    public async Task ShippingField_IsRequiredInTheBrowser_WithTheServersMessage(string fieldName)
    {
        var field = FindField(await RenderAsync(), fieldName);

        Assert.Matches("\\srequired[\\s=>]", field);
        Assert.Contains($"data-msg-required=\"{CheckoutAddress.ShippingRequiredMessages[fieldName]}\"", field);
    }

    // Pickup disables the address fieldset, which jQuery Validate skips along with hidden fields.
    [Fact]
    public async Task ShippingFields_SitInAFieldsetDisabledForPickup()
    {
        var html = await RenderAsync();
        var fieldset = Regex.Match(html, "<fieldset\\b[^>]*>(.*?)</fieldset>", RegexOptions.Singleline);

        Assert.Contains($":disabled=\"method !== '{FulfillmentMethod.Shipping}'\"", fieldset.Value);
        Assert.All(CheckoutAddress.ShippingRequiredMessages.Keys, name => Assert.Single(Fields(fieldset.Groups[1].Value), f => f.Contains($"name=\"{name}\"")));
    }

    // A failed post re-renders from ModelState's attempted values, which the select tag helper
    // prefers over the model; exactly one option must end up selected either way.
    [Theory]
    [InlineData("United States", "United States")]
    [InlineData("Narnia", "United States")]
    public async Task Country_OnCheckoutPage_SelectsPostedCountryOrDefault(string posted, string expected)
    {
        var modelState = new ModelStateDictionary();
        modelState.SetModelValue("Address.Country", posted, posted);

        var options = Options(await RenderOnCheckoutPageAsync(modelState));

        var selected = Assert.Single(options, o => o.Selected);
        Assert.Equal(expected, selected.Value);
    }

    private static string FindField(string html, string name) =>
        Assert.Single(Fields(html), f => f.Contains($"name=\"{name}\""));

    // The opening tag of every form control: the text inputs and the country select.
    private static IEnumerable<string> Fields(string html) =>
        Regex.Matches(html, "<(?:input|select)\\b[^>]*>").Select(m => m.Value);

    private static IEnumerable<(string Value, bool Selected)> Options(string html) =>
        Regex.Matches(html, "<option\\b([^>]*)>").Select(m => (
            Regex.Match(m.Groups[1].Value, "value=\"([^\"]*)\"").Groups[1].Value,
            Regex.IsMatch(m.Groups[1].Value, "\\bselected\\b")));

    private static Task<string> RenderAsync(CheckoutAddress? model = null) =>
        RazorViewToStringRendererTests.CreateRenderer()
            .RenderAsync("~/Pages/Checkout/_CheckoutDetailsFields.cshtml", model ?? new CheckoutAddress());

    // Runs the partial tag helper exactly as Index.cshtml's <partial for="Address"> does,
    // against the page's own IndexModel and ModelState.
    private static async Task<string> RenderOnCheckoutPageAsync(ModelStateDictionary modelState)
    {
        var services = RazorViewToStringRendererTests.CreateRazorServices();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);

        var viewData = new ViewDataDictionary<IndexModel>(services.GetRequiredService<IModelMetadataProvider>(), modelState)
        {
            Model = new IndexModel(cartService: null!, checkoutSessionStore: null!),
        };
        var viewContext = new ViewContext(
            actionContext,
            new HostPageView(),
            viewData,
            new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>()),
            TextWriter.Null,
            new HtmlHelperOptions());

        var partial = new PartialTagHelper(
            services.GetRequiredService<ICompositeViewEngine>(),
            services.GetRequiredService<IViewBufferScope>())
        {
            Name = "~/Pages/Checkout/_CheckoutDetailsFields.cshtml",
            For = services.GetRequiredService<ModelExpressionProvider>().CreateModelExpression(viewData, m => m.Address),
            ViewContext = viewContext,
        };

        var context = new TagHelperContext([], new Dictionary<object, object>(), "partial");
        var output = new TagHelperOutput("partial", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await partial.ProcessAsync(context, output);

        await using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
