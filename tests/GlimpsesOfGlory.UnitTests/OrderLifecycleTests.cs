using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.UnitTests;

// OrderLifecycle's rules in memory. The SQL form of each rule (OnOrders) is checked against these
// in GlimpsesOfGlory.IntegrationTests' OrderRuleSqlTests.
public sealed class OrderLifecycleTests
{
    [Theory]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, true)]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, false)]
    public void MarkShipped_OnlyForNewShippingOrders(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkShipped.IsAllowed(Facts(status, fulfillmentMethod)));
    }

    [Theory]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, true)]
    [InlineData(OrderStatus.ReadyForPickup, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, false)]
    public void MarkReadyForPickup_OnlyForNewPickupOrders(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkReadyForPickup.IsAllowed(Facts(status, fulfillmentMethod)));
    }

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    public void MarkCancelled_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkCancelled.IsAllowed(Facts(status)));
    }

    [Theory]
    [InlineData(OrderStatus.ReadyForPickup, FulfillmentMethod.Pickup, true)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, false)]
    public void MarkPickedUp_OnlyFromReadyForPickup(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkPickedUp.IsAllowed(Facts(status, fulfillmentMethod)));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.New, true, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.ReadyForPickup, false, true)]
    [InlineData(OrderStatus.PickedUp, false, true)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    public void MarkPaid_WhenUnpaidAndNotCancelled(OrderStatus status, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkPaid.IsAllowed(Facts(status, isPaid: isPaid)));
    }

    [Fact]
    public void Transitions_MoveToTheirStep_AndStampItsTimestamp()
    {
        Assert.Equal(OrderStatus.Shipped, OrderLifecycle.MarkShipped.TargetStatus);
        Assert.Equal(OrderStatus.ReadyForPickup, OrderLifecycle.MarkReadyForPickup.TargetStatus);
        Assert.Equal(OrderStatus.PickedUp, OrderLifecycle.MarkPickedUp.TargetStatus);
        Assert.Equal(OrderStatus.Cancelled, OrderLifecycle.MarkCancelled.TargetStatus);
        Assert.Null(OrderLifecycle.MarkPaid.TargetStatus);

        Assert.Equal(nameof(Order.ShippedAt), StampedProperty(OrderLifecycle.MarkShipped));
        Assert.Equal(nameof(Order.ReadyForPickupAt), StampedProperty(OrderLifecycle.MarkReadyForPickup));
        Assert.Equal(nameof(Order.PickedUpAt), StampedProperty(OrderLifecycle.MarkPickedUp));
        Assert.Equal(nameof(Order.CancelledAt), StampedProperty(OrderLifecycle.MarkCancelled));
        Assert.Equal(nameof(Order.PaidAt), StampedProperty(OrderLifecycle.MarkPaid));
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
        Assert.Equal(expected, OrderLifecycle.NeedsRefund.IsSatisfiedBy(Facts(status, paymentMethod: paymentMethod, isPaid: isPaid)));
    }

    [Theory]
    [InlineData(OrderStatus.New, true, true)]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.ReadyForPickup, false, true)]
    [InlineData(OrderStatus.ReadyForPickup, true, false)]
    [InlineData(OrderStatus.PickedUp, false, true)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.Cancelled, true, false)]
    public void NeedsAction_ForNewOrUnpaidOpenOrders(OrderStatus status, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.NeedsAction.IsSatisfiedBy(Facts(status, isPaid: isPaid)));
    }

    [Theory]
    [InlineData(OrderStatus.New, PaymentMethod.Cash, false, true)]
    [InlineData(OrderStatus.Shipped, PaymentMethod.Cash, false, true)]
    [InlineData(OrderStatus.PickedUp, PaymentMethod.Cash, false, true)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Cash, false, false)]
    [InlineData(OrderStatus.New, PaymentMethod.Cash, true, false)]
    [InlineData(OrderStatus.New, PaymentMethod.Card, false, false)]
    public void UnpaidCash_ForOpenCashOrdersNotYetPaid(OrderStatus status, PaymentMethod paymentMethod, bool isPaid, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.UnpaidCash.IsSatisfiedBy(Facts(status, paymentMethod: paymentMethod, isPaid: isPaid)));
    }

    [Theory]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, true)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, true)]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.ReadyForPickup, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, false)]
    public void ToFulfill_OnlyNewOrders(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.ToFulfill.IsSatisfiedBy(Facts(status, fulfillmentMethod)));
    }

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.PickedUp, true)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanResendConfirmation_UnlessCancelled(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.CanResendConfirmation.IsSatisfiedBy(Facts(status)));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.ReadyForPickup, false, true)]
    [InlineData(OrderStatus.PickedUp, false, false)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    public void FulfilmentEmails_OnlyResendWhileInTheirStep(OrderStatus status, bool shippedEmail, bool readyForPickupEmail)
    {
        Assert.Equal(shippedEmail, OrderLifecycle.CanResendShippedEmail.IsSatisfiedBy(Facts(status)));
        Assert.Equal(readyForPickupEmail, OrderLifecycle.CanResendReadyForPickupEmail.IsSatisfiedBy(Facts(status)));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
    [InlineData(OrderStatus.ReadyForPickup, true, false)]
    [InlineData(OrderStatus.PickedUp, true, false)]
    [InlineData(OrderStatus.Cancelled, false, true)]
    public void TabRules_SplitByStatus(OrderStatus status, bool shippedOrCollected, bool cancelled)
    {
        Assert.Equal(shippedOrCollected, OrderLifecycle.ShippedOrCollected.IsSatisfiedBy(Facts(status)));
        Assert.Equal(cancelled, OrderLifecycle.Cancelled.IsSatisfiedBy(Facts(status)));
    }

    [Fact]
    public void FactsOf_ReadsTheOrder()
    {
        var order = NewOrder(OrderStatus.ReadyForPickup, FulfillmentMethod.Pickup, PaymentMethod.Cash, isPaid: true);

        Assert.Equal(Facts(OrderStatus.ReadyForPickup, FulfillmentMethod.Pickup, PaymentMethod.Cash, isPaid: true), OrderFacts.Of(order));
    }

    // Each flag must read the rule it's named after (CanMarkShipped -> MarkShipped, NeedsRefund ->
    // NeedsRefund), over every combination of facts - so two flags can't be wired the wrong way round.
    [Fact]
    public void Actions_EachFlagReadsTheRuleItIsNamedAfter()
    {
        var flags = typeof(AdminOrderActions).GetProperties();
        Assert.NotEmpty(flags);

        foreach (var facts in
            from status in Enum.GetValues<OrderStatus>()
            from fulfillmentMethod in Enum.GetValues<FulfillmentMethod>()
            from paymentMethod in Enum.GetValues<PaymentMethod>()
            from isPaid in BothPaidStates
            select Facts(status, fulfillmentMethod, paymentMethod, isPaid))
        {
            var actions = OrderLifecycle.Actions(facts);
            foreach (var flag in flags)
            {
                var rule = RulesByName.TryGetValue(flag.Name, out var named) ? named : RulesByName[flag.Name["Can".Length..]];
                Assert.True(rule.IsSatisfiedBy(facts) == (bool)flag.GetValue(actions)!, $"{flag.Name} disagrees with its rule for {facts}");
            }
        }
    }

    private static readonly bool[] BothPaidStates = [false, true];

    private static IReadOnlyDictionary<string, OrderRule> RulesByName => OrderLifecycleRules.ByName;

    private static OrderFacts Facts(
        OrderStatus status,
        FulfillmentMethod fulfillmentMethod = FulfillmentMethod.Shipping,
        PaymentMethod paymentMethod = PaymentMethod.Card,
        bool isPaid = false) =>
        new()
        {
            Status = status,
            FulfillmentMethod = fulfillmentMethod,
            PaymentMethod = paymentMethod,
            IsPaid = isPaid,
        };

    private static Order NewOrder(OrderStatus status, FulfillmentMethod fulfillmentMethod, PaymentMethod paymentMethod, bool isPaid) => new()
    {
        Email = "shopper@example.com",
        ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
        Status = status,
        FulfillmentMethod = fulfillmentMethod,
        PaymentMethod = paymentMethod,
        PaidAt = isPaid ? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero) : null,
    };

    private static string StampedProperty(OrderTransition transition) =>
        ((System.Linq.Expressions.MemberExpression)transition.StepTimestamp.Body).Member.Name;
}
