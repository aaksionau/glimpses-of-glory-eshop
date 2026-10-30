namespace GlimpsesOfGlory.Abstractions.Dtos;

// For pickup orders the street address fields are null. Phone is optional for every order.
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
