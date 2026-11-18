using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.UnitTests;

public class ShippingCalculatorTests
{
    private static readonly ShippingRate Poster = new(FirstItem: 5.00m, AdditionalItem: 1.00m);
    private static readonly ShippingRate Mug = new(FirstItem: 8.00m, AdditionalItem: 3.00m);

    [Fact]
    public void Calculate_IsZero_ForNoItems()
    {
        Assert.Equal(0m, ShippingCalculator.Calculate([]));
    }

    [Fact]
    public void Calculate_IgnoresLinesWithNoQuantity()
    {
        Assert.Equal(0m, ShippingCalculator.Calculate([new ShippingItem(Mug, 0)]));
    }

    [Theory]
    [InlineData(1, 5.00)]
    [InlineData(2, 6.00)]
    [InlineData(5, 9.00)]
    public void Calculate_OneProduct_PaysFirstItemThenAdditionalPerExtraUnit(int quantity, decimal expected)
    {
        Assert.Equal(expected, ShippingCalculator.Calculate([new ShippingItem(Poster, quantity)]));
    }

    [Fact]
    public void Calculate_MixedCart_HighestFirstItemRateOpensTheParcel()
    {
        // Mug opens at $8; both posters ride along at $1 each.
        var cost = ShippingCalculator.Calculate([new ShippingItem(Poster, 2), new ShippingItem(Mug, 1)]);

        Assert.Equal(10.00m, cost);
    }

    [Fact]
    public void Calculate_MixedCart_ExtraUnitsOfTheOpenerPayItsAdditionalRate()
    {
        // Mug opens at $8, the second mug $3, the poster $1.
        var cost = ShippingCalculator.Calculate([new ShippingItem(Poster, 1), new ShippingItem(Mug, 2)]);

        Assert.Equal(12.00m, cost);
    }

    [Fact]
    public void Calculate_TieOnFirstItemRate_OpensWithTheHigherAdditionalRate()
    {
        var cheapExtra = new ShippingRate(FirstItem: 6.00m, AdditionalItem: 0.50m);
        var dearExtra = new ShippingRate(FirstItem: 6.00m, AdditionalItem: 4.00m);

        // dearExtra opens ($6) so it doesn't pay its $4; cheapExtra pays $0.50.
        var cost = ShippingCalculator.Calculate([new ShippingItem(cheapExtra, 1), new ShippingItem(dearExtra, 1)]);

        Assert.Equal(6.50m, cost);
    }
}
