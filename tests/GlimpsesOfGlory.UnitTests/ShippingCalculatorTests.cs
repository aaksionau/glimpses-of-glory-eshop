using GlimpsesOfGlory.Domain;

namespace GlimpsesOfGlory.UnitTests;

public class ShippingCalculatorTests
{
    private static ShippingCalculator CreateCalculator() => new(
    [
        new ShippingTier(MinQuantity: 1, Amount: 5.00m),
        new ShippingTier(MinQuantity: 5, Amount: 9.00m),
    ]);

    [Theory]
    [InlineData(0, 0.00)]
    [InlineData(1, 5.00)]
    [InlineData(4, 5.00)]
    [InlineData(5, 9.00)]
    [InlineData(100, 9.00)]
    public void Calculate_ReturnsAmountForTierMatchingTotalQuantity(int totalQuantity, decimal expectedAmount)
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(totalQuantity);

        Assert.Equal(expectedAmount, result);
    }

    [Fact]
    public void Constructor_ThrowsWhenNoTiersProvided()
    {
        Assert.Throws<ArgumentException>(() => new ShippingCalculator([]));
    }
}
