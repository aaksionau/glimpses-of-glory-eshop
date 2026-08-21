namespace GlimpsesOfGlory.Abstractions.Dtos;

public sealed record ShippingAddressInfo(
    string Email,
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);
