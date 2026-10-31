using System.Linq.Expressions;
using System.Reflection;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// The order fields the lifecycle rules read. Each rule is written once over these facts and
// FromOrder says how each fact is read off an order, so the same rule runs in memory and in SQL.
public sealed record OrderFacts
{
    public required OrderStatus Status { get; init; }
    public required FulfillmentMethod FulfillmentMethod { get; init; }
    public required PaymentMethod PaymentMethod { get; init; }
    public required bool IsPaid { get; init; }

    // SQL-translatable, so it also serves as a query projection.
    public static readonly Expression<Func<Order, OrderFacts>> FromOrder = o => new OrderFacts
    {
        Status = o.Status,
        FulfillmentMethod = o.FulfillmentMethod,
        PaymentMethod = o.PaymentMethod,
        IsPaid = o.PaidAt != null,
    };

    private static readonly Func<Order, OrderFacts> fromOrder = FromOrder.Compile();

    public static OrderFacts Of(Order order) => fromOrder(order);

    // Rewrites a rule over the facts into the same rule over an order's columns.
    internal static Expression<Func<Order, bool>> OnOrders(Expression<Func<OrderFacts, bool>> predicate) =>
        Expression.Lambda<Func<Order, bool>>(
            new Substitution(predicate.Parameters[0]).Visit(predicate.Body),
            FromOrder.Parameters);

    // Swaps each fact for the column expression FromOrder reads it from.
    private sealed class Substitution(ParameterExpression facts) : ExpressionVisitor
    {
        private static readonly Dictionary<MemberInfo, Expression> Columns =
            ((MemberInitExpression)FromOrder.Body).Bindings
                .Cast<MemberAssignment>()
                .ToDictionary(b => b.Member, b => b.Expression);

        protected override Expression VisitMember(MemberExpression node) =>
            node.Expression == facts ? Columns[node.Member] : base.VisitMember(node);
    }
}
