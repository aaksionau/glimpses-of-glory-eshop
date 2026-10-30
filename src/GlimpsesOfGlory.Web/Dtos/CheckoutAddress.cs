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

        (string? Value, string Name, string Message)[] requiredFields =
        [
            (AddressLine1, nameof(AddressLine1), "Address is required."),
            (City, nameof(City), "City is required."),
            (State, nameof(State), "State/region is required."),
            (PostalCode, nameof(PostalCode), "Postal code is required."),
            (Country, nameof(Country), "Country is required."),
        ];

        foreach (var (value, name, message) in requiredFields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult(message, [name]);
            }
        }

        // The form only offers supported countries, but the post can still carry anything.
        if (!string.IsNullOrWhiteSpace(Country) && !ShippingCountryPolicy.IsSupported(Country))
        {
            yield return new ValidationResult("Choose a country we ship to.", [nameof(Country)]);
        }
    }
}
