using System.Linq.Expressions;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore.Query;

namespace GlimpsesOfGlory.Core.Services;

// One admin action on an order: which orders allow it, the status it moves them to, and the step
// timestamp it records. Declared in OrderLifecycle and applied by AdminOrderService.
public sealed class OrderTransition(OrderRule guard, OrderStatus? targetStatus, Expression<Func<Order, DateTimeOffset?>> stepTimestamp)
{
    public OrderRule Guard { get; } = guard;

    // Null when the action leaves the status as it is.
    public OrderStatus? TargetStatus { get; } = targetStatus;

    public Expression<Func<Order, DateTimeOffset?>> StepTimestamp { get; } = stepTimestamp;

    public bool IsAllowed(OrderFacts facts) => Guard.IsSatisfiedBy(facts);

    // The step timestamp is set as "existing value ?? now" so a repeat never moves one that's
    // already recorded.
    internal void Apply(UpdateSettersBuilder<Order> setters, DateTimeOffset now)
    {
        if (TargetStatus is { } status)
        {
            setters.SetProperty(o => o.Status, status);
        }

        // Read through a closure so EF sends now as a parameter, not an inlined literal.
        Expression<Func<DateTimeOffset?>> nowValue = () => now;
        var stampOnce = Expression.Lambda<Func<Order, DateTimeOffset?>>(
            Expression.Coalesce(StepTimestamp.Body, nowValue.Body),
            StepTimestamp.Parameters);
        setters.SetProperty(StepTimestamp, stampOnce);
    }
}
