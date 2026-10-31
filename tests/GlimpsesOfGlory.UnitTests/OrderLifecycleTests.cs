using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.UnitTests;

public sealed class OrderLifecycleTests : IDisposable
{
    private readonly SqliteTestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    public void MarkShipped_OnlyFromNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkShipped.IsAllowed(Facts(status)));
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
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Pickup, true)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, false)]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, false)]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, false)]
    public void MarkPickedUp_OnlyForPickupOrdersReadyForPickup(OrderStatus status, FulfillmentMethod fulfillmentMethod, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.MarkPickedUp.IsAllowed(Facts(status, fulfillmentMethod)));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, true)]
    [InlineData(OrderStatus.Shipped, false, true)]
    [InlineData(OrderStatus.Cancelled, false, false)]
    [InlineData(OrderStatus.New, true, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
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
        Assert.Equal(OrderStatus.PickedUp, OrderLifecycle.MarkPickedUp.TargetStatus);
        Assert.Equal(OrderStatus.Cancelled, OrderLifecycle.MarkCancelled.TargetStatus);
        Assert.Null(OrderLifecycle.MarkPaid.TargetStatus);

        Assert.Equal(nameof(Order.ShippedAt), StampedProperty(OrderLifecycle.MarkShipped));
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
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void ToFulfill_OnlyNewOrders(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.ToFulfill.IsSatisfiedBy(Facts(status)));
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
    [InlineData(OrderStatus.New, false)]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.PickedUp, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void CanResendShippedEmail_OnlyWhileShippedOrReady(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderLifecycle.CanResendShippedEmail.IsSatisfiedBy(Facts(status)));
    }

    [Theory]
    [InlineData(OrderStatus.New, false, false)]
    [InlineData(OrderStatus.Shipped, true, false)]
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
        var order = NewOrder(OrderStatus.Shipped, FulfillmentMethod.Pickup, PaymentMethod.Cash, isPaid: true);

        Assert.Equal(Facts(OrderStatus.Shipped, FulfillmentMethod.Pickup, PaymentMethod.Cash, isPaid: true), OrderFacts.Of(order));
    }

    public static TheoryData<string> Rules() => new(RulesByName.Keys);

    // The SQL form of every rule must pick exactly the orders its in-memory form accepts, over every
    // combination of facts - this is what keeps dashboard counts and list tabs in step with the badges.
    [Theory]
    [MemberData(nameof(Rules))]
    public async Task OnOrders_MatchesIsSatisfiedBy_ForEveryCombinationOfFacts(string ruleName)
    {
        var rule = RulesByName[ruleName];
        await using (var seed = database.CreateDb())
        {
            seed.Orders.AddRange(
                from status in Enum.GetValues<OrderStatus>()
                from fulfillmentMethod in Enum.GetValues<FulfillmentMethod>()
                from paymentMethod in Enum.GetValues<PaymentMethod>()
                from isPaid in new[] { false, true }
                select NewOrder(status, fulfillmentMethod, paymentMethod, isPaid));
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDb();
        var orders = await db.Orders.AsNoTracking().ToListAsync();
        var inMemory = orders.Where(o => rule.IsSatisfiedBy(OrderFacts.Of(o))).Select(o => o.Id).Order().ToList();
        var inSql = await db.Orders.Where(rule.OnOrders).Select(o => o.Id).OrderBy(id => id).ToListAsync();

        Assert.Equal(inMemory, inSql);
    }

    private static readonly Dictionary<string, OrderRule> RulesByName = new()
    {
        [nameof(OrderLifecycle.MarkShipped)] = OrderLifecycle.MarkShipped.Guard,
        [nameof(OrderLifecycle.MarkPickedUp)] = OrderLifecycle.MarkPickedUp.Guard,
        [nameof(OrderLifecycle.MarkCancelled)] = OrderLifecycle.MarkCancelled.Guard,
        [nameof(OrderLifecycle.MarkPaid)] = OrderLifecycle.MarkPaid.Guard,
        [nameof(OrderLifecycle.CanResendConfirmation)] = OrderLifecycle.CanResendConfirmation,
        [nameof(OrderLifecycle.CanResendShippedEmail)] = OrderLifecycle.CanResendShippedEmail,
        [nameof(OrderLifecycle.NeedsRefund)] = OrderLifecycle.NeedsRefund,
        [nameof(OrderLifecycle.ToFulfill)] = OrderLifecycle.ToFulfill,
        [nameof(OrderLifecycle.UnpaidCash)] = OrderLifecycle.UnpaidCash,
        [nameof(OrderLifecycle.NeedsAction)] = OrderLifecycle.NeedsAction,
        [nameof(OrderLifecycle.ShippedOrCollected)] = OrderLifecycle.ShippedOrCollected,
        [nameof(OrderLifecycle.Cancelled)] = OrderLifecycle.Cancelled,
    };

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
