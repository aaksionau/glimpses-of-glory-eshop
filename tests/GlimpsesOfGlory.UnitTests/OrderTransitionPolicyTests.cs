using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.UnitTests;

public class OrderTransitionPolicyTests
{
    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanMarkShipped_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkShipped(status));
    }

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanMarkCancelled_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkCancelled(status));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.New, true, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    public void CanMarkPaid_WhenUnpaidAndNotCancelled(OrderStatus status, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkPaid(status, isPaid));
    }
}
