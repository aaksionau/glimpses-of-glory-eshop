using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class PaymentLabelsTests
{
    [Theory]
    [InlineData(PaymentMethod.Card, true, "Card")]
    [InlineData(PaymentMethod.Cash, false, "Cash at pickup (unpaid)")]
    [InlineData(PaymentMethod.Cash, true, "Cash (paid)")]
    public void Status_DescribesPaymentMethodAndWhetherCollected(PaymentMethod paymentMethod, bool isPaid, string expected)
    {
        Assert.Equal(expected, PaymentLabels.Status(paymentMethod, isPaid));
    }
}
