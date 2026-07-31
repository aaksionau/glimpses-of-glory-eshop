using System.ComponentModel.DataAnnotations;

namespace GlimpsesOfGlory.Web.Checkout;

public sealed class CheckoutAddress
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State/region is required.")]
    [StringLength(100)]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;
}
