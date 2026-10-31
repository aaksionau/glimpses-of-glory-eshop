using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.TestSupport;

// Shared inputs for the tests that walk OrderLifecycle's rules in memory and in SQL.
internal static class OrderLifecycleRules
{
    // Every rule OrderLifecycle declares, by field name (a transition contributes its guard). Read off
    // OrderLifecycle itself, so a newly declared rule or transition can't skip the tests that walk them.
    public static readonly IReadOnlyDictionary<string, OrderRule> ByName = typeof(OrderLifecycle)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .ToDictionary(f => f.Name, f => f.GetValue(null) switch
        {
            OrderRule rule => rule,
            OrderTransition transition => transition.Guard,
            var other => throw new InvalidOperationException($"Unexpected OrderLifecycle member {f.Name}: {other}"),
        });

    private static readonly bool[] BothPaidStates = [false, true];

    // Every combination of facts an order can have, for tests that check a rule over all of them.
    public static IEnumerable<OrderFacts> AllFacts() =>
        from status in Enum.GetValues<OrderStatus>()
        from fulfillmentMethod in Enum.GetValues<FulfillmentMethod>()
        from paymentMethod in Enum.GetValues<PaymentMethod>()
        from isPaid in BothPaidStates
        select new OrderFacts
        {
            Status = status,
            FulfillmentMethod = fulfillmentMethod,
            PaymentMethod = paymentMethod,
            IsPaid = isPaid,
        };

    // An order whose OrderFacts.Of reads back as the given facts.
    public static Order OrderWith(OrderFacts facts) => new()
    {
        Email = "shopper@example.com",
        ShippingAddress = new ShippingAddress { FullName = "Jane Shopper" },
        Status = facts.Status,
        FulfillmentMethod = facts.FulfillmentMethod,
        PaymentMethod = facts.PaymentMethod,
        PaidAt = facts.IsPaid ? new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero) : null,
    };
}
