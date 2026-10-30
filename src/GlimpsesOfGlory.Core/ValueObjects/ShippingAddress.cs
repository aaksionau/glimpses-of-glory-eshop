using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Core.ValueObjects;

// Pickup orders still carry the customer's name (and optional phone) here, but leave the street
// address fields null.
public sealed class ShippingAddress
{
    public required string FullName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }

    public static ShippingAddress FromInfo(ShippingAddressInfo info) => new()
    {
        FullName = info.FullName,
        AddressLine1 = info.AddressLine1,
        AddressLine2 = info.AddressLine2,
        City = info.City,
        State = info.State,
        PostalCode = info.PostalCode,
        Country = info.Country,
        Phone = info.Phone,
    };

    public ShippingAddressInfo ToInfo(string email) =>
        new(email, FullName, AddressLine1, AddressLine2, City, State, PostalCode, Country, Phone);

    // EF owned entities can only belong to one owner, so copying a ShippingAddress from
    // one entity onto another (PendingCheckout -> Order) needs a fresh instance rather
    // than reusing the same tracked object.
    public ShippingAddress Clone() => (ShippingAddress)MemberwiseClone();
}
