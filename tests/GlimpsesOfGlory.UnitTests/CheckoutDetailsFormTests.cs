using System.Text.RegularExpressions;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutDetailsFormTests
{
    // The standard autofill tokens browsers and password managers use to fill the
    // contact and address fields in one go.
    [Theory]
    [InlineData("Email", "email")]
    [InlineData("FullName", "name")]
    [InlineData("AddressLine1", "address-line1")]
    [InlineData("AddressLine2", "address-line2")]
    [InlineData("City", "address-level2")]
    [InlineData("State", "address-level1")]
    [InlineData("PostalCode", "postal-code")]
    [InlineData("Country", "country-name")]
    public async Task DetailsInput_HasAutocompleteToken(string fieldName, string token)
    {
        var input = FindInput(await RenderAsync(), fieldName);

        Assert.Contains($"autocomplete=\"{token}\"", input);
    }

    [Fact]
    public async Task EveryDetailsInput_HasAnAutocompleteToken()
    {
        var html = await RenderAsync();

        var inputs = Regex.Matches(html, "<input\\b[^>]*>")
            .Select(m => m.Value)
            .ToList();

        Assert.Equal(8, inputs.Count);
        Assert.All(inputs, i => Assert.Contains("autocomplete=\"", i));
    }

    private static string FindInput(string html, string fieldName) =>
        Assert.Single(
            Regex.Matches(html, "<input\\b[^>]*>").Select(m => m.Value),
            i => i.Contains($"name=\"{fieldName}\""));

    private static Task<string> RenderAsync() =>
        RazorViewToStringRendererTests.CreateRenderer()
            .RenderAsync("~/Pages/Checkout/_CheckoutDetailsFields.cshtml", new CheckoutAddress());
}
