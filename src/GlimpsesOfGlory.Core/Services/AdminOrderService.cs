using System.Linq.Expressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.Core.Services;

public sealed class AdminOrderService(AppDbContext db, IInventoryStore inventoryStore, IPaymentGateway paymentGateway, IEmailSender emailSender, TimeProvider timeProvider, ILogger<AdminOrderService> logger) : IAdminOrderService
{
    // SQL-translatable twin of OrderTransitionPolicy.NeedsAction - keep the two in step.
    private static readonly Expression<Func<Order, bool>> NeedsActionFilter =
        o => o.Status == OrderStatus.New || (o.PaidAt == null && o.Status != OrderStatus.Cancelled);

    public async Task<IReadOnlyList<AdminOrderSummary>> GetOrdersAsync(AdminOrderTab tab, CancellationToken cancellationToken)
    {
        return await InTab(db.Orders.AsNoTracking(), tab)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderSummary(
                o.Id,
                o.ShippingAddress.FullName,
                o.Email,
                o.Lines.Sum(l => l.Quantity),
                o.Total,
                o.Status,
                o.FulfillmentMethod,
                o.PaidAt != null,
                o.Lines.Any(l => l.IsPreorder),
                OrderTransitionPolicy.NeedsRefund(o.Status, o.PaymentMethod, o.PaidAt != null),
                OrderTransitionPolicy.NeedsAction(o.Status, o.PaidAt != null),
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    // One COUNT query per tab; the DbContext can't run them concurrently.
    public async Task<AdminOrderTabCounts> GetOrderTabCountsAsync(CancellationToken cancellationToken)
    {
        var needsAction = await InTab(db.Orders, AdminOrderTab.NeedsAction).CountAsync(cancellationToken);
        var shipped = await InTab(db.Orders, AdminOrderTab.Shipped).CountAsync(cancellationToken);
        var cancelled = await InTab(db.Orders, AdminOrderTab.Cancelled).CountAsync(cancellationToken);
        var all = await db.Orders.CountAsync(cancellationToken);
        return new AdminOrderTabCounts(needsAction, shipped, cancelled, all);
    }

    private static IQueryable<Order> InTab(IQueryable<Order> orders, AdminOrderTab tab) => tab switch
    {
        AdminOrderTab.NeedsAction => orders.Where(NeedsActionFilter),
        AdminOrderTab.Shipped => orders.Where(o => o.Status == OrderStatus.Shipped || o.Status == OrderStatus.PickedUp),
        AdminOrderTab.Cancelled => orders.Where(o => o.Status == OrderStatus.Cancelled),
        AdminOrderTab.All => orders,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
    };

    public async Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : ToDetail(order);
    }

    public async Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken)
    {
        // Loaded up front because the shipping email needs the full order and its lines.
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return OrderUpdateResult.NotFound;
        }
        if (!OrderTransitionPolicy.CanMarkShipped(order.Status))
        {
            return OrderUpdateResult.InvalidTransition;
        }

        // Guarded on the status we just checked, so a double-click (or two admins at once)
        // only ships - and emails the buyer - once.
        var tracking = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber;
        var now = timeProvider.GetUtcNow();
        var result = await UpdateIfStatusAsync(id, order.Status, setters => setters
            .SetProperty(o => o.Status, OrderStatus.Shipped)
            .SetProperty(o => o.TrackingNumber, tracking)
            .SetProperty(o => o.ShippedAt, o => o.ShippedAt ?? now), cancellationToken);
        if (result != OrderUpdateResult.Updated)
        {
            return result;
        }

        order.Status = OrderStatus.Shipped;
        order.TrackingNumber = tracking;
        try
        {
            await emailSender.SendShippedNotificationAsync(ToShippedView(order), cancellationToken);
        }
        catch (Exception ex)
        {
            // The status change is already committed - a failed notification email
            // shouldn't undo it. Log for manual follow-up instead.
            logger.LogError(ex, "Failed to send shipping-notification email for order {OrderId}", order.Id);
        }

        return OrderUpdateResult.Updated;
    }

    public async Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(id, cancellationToken);
        if (state is null)
        {
            return OrderUpdateResult.NotFound;
        }
        if (!OrderTransitionPolicy.CanMarkPickedUp(state.Status, state.FulfillmentMethod))
        {
            return OrderUpdateResult.InvalidTransition;
        }

        var now = timeProvider.GetUtcNow();
        return await UpdateIfStatusAsync(id, state.Status, setters => setters
            .SetProperty(o => o.Status, OrderStatus.PickedUp)
            .SetProperty(o => o.PickedUpAt, o => o.PickedUpAt ?? now), cancellationToken);
    }

    public async Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(id, cancellationToken);
        if (state is null)
        {
            return OrderUpdateResult.NotFound;
        }
        if (!OrderTransitionPolicy.CanMarkPaid(state.Status, state.IsPaid))
        {
            return OrderUpdateResult.InvalidTransition;
        }

        // Coalesced so a concurrent repeat click doesn't move the recorded payment date.
        var now = timeProvider.GetUtcNow();
        return await UpdateIfStatusAsync(id, state.Status, setters => setters
            .SetProperty(o => o.PaidAt, o => o.PaidAt ?? now), cancellationToken);
    }

    public async Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(id, cancellationToken);
        if (state is null)
        {
            return OrderUpdateResult.NotFound;
        }
        if (!OrderTransitionPolicy.CanMarkCancelled(state.Status))
        {
            return OrderUpdateResult.InvalidTransition;
        }

        // The status guard and the restock commit together, so a concurrent repeat click that
        // loses the guard can't restock a second time, and a failed restock leaves the order New.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var result = await UpdateIfStatusAsync(id, state.Status, setters => setters
            .SetProperty(o => o.Status, OrderStatus.Cancelled)
            .SetProperty(o => o.CancelledAt, o => o.CancelledAt ?? now), cancellationToken);
        if (result != OrderUpdateResult.Updated)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        var lines = await db.OrderLines
            .AsNoTracking()
            .Where(l => l.OrderId == id)
            .ToListAsync(cancellationToken);
        await CancelledOrderRestocker.RestockAsync(inventoryStore, lines, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return OrderUpdateResult.Updated;
    }

    private Task<OrderState?> GetStateAsync(int id, CancellationToken cancellationToken) =>
        db.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderState(o.Status, o.FulfillmentMethod, o.PaidAt != null))
            .FirstOrDefaultAsync(cancellationToken);

    // Only applies if the order is still in the status the caller checked the policy against,
    // so a concurrent change (another tab, another admin) can't be overridden. Step timestamps are
    // set as "existing value ?? now" so a repeat call never moves one that's already recorded.
    private async Task<OrderUpdateResult> UpdateIfStatusAsync(int id, OrderStatus expectedStatus, Action<UpdateSettersBuilder<Order>> setters, CancellationToken cancellationToken)
    {
        var rows = await db.Orders
            .Where(o => o.Id == id && o.Status == expectedStatus)
            .ExecuteUpdateAsync(setters, cancellationToken);

        return rows > 0 ? OrderUpdateResult.Updated : OrderUpdateResult.InvalidTransition;
    }

    private AdminOrderDetail ToDetail(Order order) => new(
        order.Id,
        order.FulfillmentMethod,
        order.PaymentMethod,
        order.IsPaid,
        order.StripePaymentIntentId is { } paymentIntentId ? paymentGateway.GetPaymentDashboardUrl(paymentIntentId) : null,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity, l.IsPreorder, l.ExpectedAvailabilityDate)).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.Status,
        order.TrackingNumber,
        order.CreatedAt,
        order.PaidAt,
        order.ShippedAt,
        order.PickedUpAt,
        order.CancelledAt,
        OrderTransitionPolicy.CanMarkShipped(order.Status),
        OrderTransitionPolicy.CanMarkPickedUp(order.Status, order.FulfillmentMethod),
        OrderTransitionPolicy.CanMarkPaid(order.Status, order.IsPaid),
        OrderTransitionPolicy.CanMarkCancelled(order.Status),
        OrderTransitionPolicy.NeedsRefund(order.Status, order.PaymentMethod, order.IsPaid));

    private static OrderShippedView ToShippedView(Order order) => new(
        order.Id,
        order.FulfillmentMethod,
        order.IsPaid,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity, l.IsPreorder, l.ExpectedAvailabilityDate)).ToList(),
        order.Total,
        order.TrackingNumber);

    private sealed record OrderState(OrderStatus Status, FulfillmentMethod FulfillmentMethod, bool IsPaid);
}
