namespace GlimpsesOfGlory.Abstractions.Dtos;

// For pickup orders only Email and FullName are set - the street address fields are null.
public sealed record ShippingAddressInfo(
    string Email,
    string FullName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);
