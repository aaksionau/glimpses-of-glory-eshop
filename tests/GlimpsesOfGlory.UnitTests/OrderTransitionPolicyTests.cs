using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.UnitTests;

public class OrderTransitionPolicyTests
{
    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    public void CanMarkShipped_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkShipped(status));
    }

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    public void CanMarkCancelled_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkCancelled(status));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Pickup, true)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, false)]
    public void CanMarkPickedUp_OnlyForPickupOrdersReadyForPickup(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkPickedUp(status, fulfillmentMethod));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.New, true, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.PickedUp, false, true)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    public void CanMarkPaid_WhenUnpaidAndNotCancelled(OrderStatus status, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.CanMarkPaid(status, isPaid));
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Card, true, true)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Card, false, false)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Cash, true, false)]
    [InlineData(OrderStatus.New, PaymentMethod.Card, true, false)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Card, true, false)]
    [InlineData(OrderStatus.PickedUp, PaymentMethod.Card, true, false)]
    public void NeedsRefund_OnlyForCancelledPaidCardOrders(OrderStatus status, PaymentMethod paymentMethod, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.NeedsRefund(status, paymentMethod, isPaid));
    }

    [Theory]
    [InlineData(OrderStatus.New, true, true)]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.PickedUp, false, true)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.Cancelled, true, false)]
    public void NeedsAction_ForNewOrUnpaidOpenOrders(OrderStatus status, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderTransitionPolicy.NeedsAction(status, isPaid));
    }
}
