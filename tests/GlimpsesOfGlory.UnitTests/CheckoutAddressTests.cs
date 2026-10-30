using System.ComponentModel.DataAnnotations;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutAddressTests
{
    private static List<ValidationResult> Validate(CheckoutAddress address)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(address, new ValidationContext(address), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_RequiresStreetAddress_WhenShipping()
    {
        var address = new CheckoutAddress { FulfillmentMethod = FulfillmentMethod.Shipping, Email = "shopper@example.com", FullName = "Jane Shopper" };

        var results = Validate(address);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CheckoutAddress.AddressLine1)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CheckoutAddress.Country)));
    }

    private static CheckoutAddress ShippingAddress(string? country) => new()
    {
        FulfillmentMethod = FulfillmentMethod.Shipping,
        Email = "shopper@example.com",
        FullName = "Jane Shopper",
        AddressLine1 = "123 Main St",
        City = "Springfield",
        State = "IL",
        PostalCode = "62704",
        Country = country,
    };

    [Theory]
    [InlineData("USA")]
    [InlineData("united states")]
    [InlineData("Narnia")]
    public void Validate_RejectsCountryOutsideSupportedList(string country)
    {
        var results = Validate(ShippingAddress(country));

        var error = Assert.Single(results);
        Assert.Equal([nameof(CheckoutAddress.Country)], error.MemberNames);
        Assert.Equal("Choose a country we ship to.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_AcceptsSupportedCountry()
    {
        Assert.Empty(Validate(ShippingAddress(ShippingCountryPolicy.DefaultCountry)));
    }

    [Fact]
    public void Validate_IgnoresCountry_WhenPickup()
    {
        var address = ShippingAddress("Narnia");
        address.FulfillmentMethod = FulfillmentMethod.Pickup;

        Assert.Empty(Validate(address));
    }

    [Fact]
    public void Validate_OnlyRequiresNameAndEmail_WhenPickup()
    {
        var address = new CheckoutAddress { FulfillmentMethod = FulfillmentMethod.Pickup, Email = "shopper@example.com", FullName = "Jane Shopper" };

        var results = Validate(address);

        Assert.Empty(results);
    }

    [Fact]
    public void ToShippingAddressInfo_DropsStreetAddress_WhenPickup()
    {
        var address = new CheckoutAddress
        {
            FulfillmentMethod = FulfillmentMethod.Pickup,
            Email = "shopper@example.com",
            FullName = "Jane Shopper",
            AddressLine1 = "123 Main St",
            City = "Springfield",
        };

        var info = address.ToShippingAddressInfo();

        Assert.Equal("Jane Shopper", info.FullName);
        Assert.Null(info.AddressLine1);
        Assert.Null(info.City);
    }

    [Theory]
    [InlineData(FulfillmentMethod.Pickup)]
    [InlineData(FulfillmentMethod.Shipping)]
    public void Validate_PhoneIsOptional(FulfillmentMethod method)
    {
        var address = ShippingAddress(ShippingCountryPolicy.DefaultCountry);
        address.FulfillmentMethod = method;

        Assert.Empty(Validate(address));
    }

    [Theory]
    [InlineData("not a phone")]
    [InlineData("1234567890123456789012345678901")]
    public void Validate_RejectsAnInvalidPhone(string phone)
    {
        var address = ShippingAddress(ShippingCountryPolicy.DefaultCountry);
        address.Phone = phone;

        var error = Assert.Single(Validate(address));
        Assert.Equal([nameof(CheckoutAddress.Phone)], error.MemberNames);
        Assert.Equal("Enter a valid phone number.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(FulfillmentMethod.Pickup)]
    [InlineData(FulfillmentMethod.Shipping)]
    public void ToShippingAddressInfo_KeepsThePhone(FulfillmentMethod method)
    {
        var address = ShippingAddress(ShippingCountryPolicy.DefaultCountry);
        address.FulfillmentMethod = method;
        address.Phone = " (555) 010-0100 ";

        Assert.Equal("(555) 010-0100", address.ToShippingAddressInfo().Phone);
    }

    [Fact]
    public void ToShippingAddressInfo_StoresABlankPhoneAsNone()
    {
        var address = ShippingAddress(ShippingCountryPolicy.DefaultCountry);
        address.Phone = "   ";

        Assert.Null(address.ToShippingAddressInfo().Phone);
    }
}
