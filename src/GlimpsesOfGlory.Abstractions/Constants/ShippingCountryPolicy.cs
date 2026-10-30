namespace GlimpsesOfGlory.Abstractions.Constants;

// The countries checkout ships to. The store sells in USD and ships with USPS, so the list is
// the United States alone for now; add a name here to offer it in the checkout Country select.
// Values are stored on the order as-is and shown verbatim on the admin views and emails.
public static class ShippingCountryPolicy
{
    // The main market: pre-selected when the checkout form has no country yet.
    public const string DefaultCountry = "United States";

    public static IReadOnlyList<string> SupportedCountries { get; } = [DefaultCountry];

    public static bool IsSupported(string? country) =>
        country is not null && SupportedCountries.Contains(country, StringComparer.Ordinal);

    // The option to show as selected: the shopper's own choice when it's one we ship to,
    // otherwise the main market.
    public static string SelectedOrDefault(string? country) =>
        IsSupported(country) ? country! : DefaultCountry;
}
