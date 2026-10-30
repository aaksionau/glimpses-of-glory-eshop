namespace GlimpsesOfGlory.Abstractions.Dtos;

// For pickup orders only Email, FullName and (if given) Phone are set - the street address fields
// are null. Phone is optional for every order: the shop's way to reach a pickup customer, and the
// carrier's for a shipped one.
public sealed record ShippingAddressInfo(
    string Email,
    string FullName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Phone = null);
