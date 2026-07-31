using GlimpsesOfGlory.Abstractions.Orders;

namespace GlimpsesOfGlory.Core.Orders.ValueObjects;

public sealed class ShippingAddress
{
    public required string FullName { get; set; }
    public required string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string PostalCode { get; set; }
    public required string Country { get; set; }

    public static ShippingAddress FromInfo(ShippingAddressInfo info) => new()
    {
        FullName = info.FullName,
        AddressLine1 = info.AddressLine1,
        AddressLine2 = info.AddressLine2,
        City = info.City,
        State = info.State,
        PostalCode = info.PostalCode,
        Country = info.Country,
    };

    public ShippingAddressInfo ToInfo(string email) =>
        new(email, FullName, AddressLine1, AddressLine2, City, State, PostalCode, Country);

    // EF owned entities can only belong to one owner, so copying a ShippingAddress from
    // one entity onto another (PendingCheckout -> Order) needs a fresh instance rather
    // than reusing the same tracked object.
    public ShippingAddress Clone() => new()
    {
        FullName = FullName,
        AddressLine1 = AddressLine1,
        AddressLine2 = AddressLine2,
        City = City,
        State = State,
        PostalCode = PostalCode,
        Country = Country,
    };
}
