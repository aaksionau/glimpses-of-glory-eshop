using System.Linq.Expressions;
using GlimpsesOfGlory.Core.Entities;

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
        var otherBody = new ParameterSwap(other.Predicate.Parameters[0], facts).Visit(other.Predicate.Body);
        return new(Expression.Lambda<Func<OrderFacts, bool>>(combine(Predicate.Body, otherBody), facts));
    }

    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
