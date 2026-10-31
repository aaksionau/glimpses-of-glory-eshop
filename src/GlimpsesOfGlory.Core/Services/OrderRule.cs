using System.Linq.Expressions;
using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore.Query;

namespace GlimpsesOfGlory.Core.Services;

// One lifecycle rule, written once over OrderFacts: checked in memory with IsSatisfiedBy, and
// applied in a query with OnOrders. The two can't drift because both come from Predicate.
public sealed class OrderRule
{
    private readonly Func<OrderFacts, bool> isSatisfiedBy;

    public OrderRule(Expression<Func<OrderFacts, bool>> predicate)
    {
        Predicate = predicate;
        isSatisfiedBy = predicate.Compile();
        OnOrders = OrderFacts.OnOrders(predicate);
    }

    public Expression<Func<OrderFacts, bool>> Predicate { get; }

    // SQL-translatable.
    public Expression<Func<Order, bool>> OnOrders { get; }

    public bool IsSatisfiedBy(OrderFacts facts) => isSatisfiedBy(facts);

    public OrderRule And(OrderRule other) => Combine(other, Expression.AndAlso);

    public OrderRule Or(OrderRule other) => Combine(other, Expression.OrElse);

    private OrderRule Combine(OrderRule other, Func<Expression, Expression, BinaryExpression> combine)
    {
        var facts = Predicate.Parameters[0];
        var otherBody = ReplacingExpressionVisitor.Replace(other.Predicate.Parameters[0], facts, other.Predicate.Body);
        return new(Expression.Lambda<Func<OrderFacts, bool>>(combine(Predicate.Body, otherBody), facts));
    }
}
