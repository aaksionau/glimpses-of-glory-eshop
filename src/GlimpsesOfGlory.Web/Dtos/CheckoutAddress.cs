using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Dtos;

public sealed class CheckoutAddress : IValidatableObject
{
    public FulfillmentMethod FulfillmentMethod { get; set; } = FulfillmentMethod.Shipping;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    // The message for each street-address field left empty when shipping; the checkout form
    // shows the same text when the browser catches the empty field first.
    public static IReadOnlyDictionary<string, string> ShippingRequiredMessages { get; } = new Dictionary<string, string>
    {
        [nameof(AddressLine1)] = "Address is required.",
        [nameof(City)] = "City is required.",
        [nameof(State)] = "State/region is required.",
        [nameof(PostalCode)] = "Postal code is required.",
        [nameof(Country)] = "Country is required.",
    };

    public bool IsPickup => FulfillmentMethod == FulfillmentMethod.Pickup;

    // Pickup orders don't need (or keep) a street address - drop anything left over
    // from the shopper toggling between shipping and pickup.
    public ShippingAddressInfo ToShippingAddressInfo() => IsPickup
        ? new(Email, FullName, null, null, null, null, null, null)
        : new(Email, FullName, AddressLine1, AddressLine2, City, State, PostalCode, Country);

    // The street address is only required when shipping, so it's validated here rather
    // than with [Required] on each property.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsPickup)
        {
            yield break;
        }

        (string? Value, string Name)[] requiredFields =
        [
            (AddressLine1, nameof(AddressLine1)),
            (City, nameof(City)),
            (State, nameof(State)),
            (PostalCode, nameof(PostalCode)),
            (Country, nameof(Country)),
        ];

        foreach (var (value, name) in requiredFields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult(ShippingRequiredMessages[name], [name]);
            }
        }

        // The form only offers supported countries, but the post can still carry anything.
        if (!string.IsNullOrWhiteSpace(Country) && !ShippingCountryPolicy.IsSupported(Country))
        {
            yield return new ValidationResult("Choose a country we ship to.", [nameof(Country)]);
        }
    }
}
