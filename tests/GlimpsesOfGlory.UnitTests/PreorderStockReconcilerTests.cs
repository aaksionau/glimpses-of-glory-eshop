using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.UnitTests;

public class PreorderStockReconcilerTests
{
    [Theory]
    [InlineData(10, 4, 6)]
    [InlineData(10, 10, 0)]
    [InlineData(4, 10, 0)]
    [InlineData(0, 0, 0)]
    public void Reconcile_SubtractsPreorderedFromReceivedFlooredAtZero(int quantityReceived, int preorderedQuantity, int expectedStock)
    {
        var result = PreorderStockReconciler.Reconcile(quantityReceived, preorderedQuantity);

        Assert.Equal(expectedStock, result);
    }
}
