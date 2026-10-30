using System.Globalization;
using GlimpsesOfGlory.Abstractions.Constants;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.Core.Services;

public sealed class AdminOrderService(AppDbContext db, IInventoryStore inventoryStore, IPaymentGateway paymentGateway, IEmailSender emailSender, TimeProvider timeProvider, ILogger<AdminOrderService> logger) : IAdminOrderService
{
    public async Task<AdminOrderPage> GetOrdersAsync(AdminOrderTab tab, AdminOrderFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var orders = Filtered(InTab(db.Orders.AsNoTracking(), tab), filter);
        var totalCount = await orders.CountAsync(cancellationToken);
        var lastPage = AdminOrderPage.PageCount(totalCount, pageSize);
        page = Math.Clamp(page, 1, lastPage);

        // Id breaks CreatedAt ties so an order can't appear on two pages, or on none.
        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                o.Lines.Where(l => l.IsPreorder).Min(l => l.ExpectedAvailabilityDate),
                OrderTransitionPolicy.NeedsRefund(o.Status, o.PaymentMethod, o.PaidAt != null),
                OrderTransitionPolicy.NeedsAction(o.Status, o.PaidAt != null),
                o.InternalNote != null,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminOrderPage(items, page, pageSize, totalCount);
    }

    // One COUNT query per tab; the DbContext can't run them concurrently.
    public async Task<AdminOrderTabCounts> GetOrderTabCountsAsync(AdminOrderFilter filter, CancellationToken cancellationToken)
    {
        var orders = Filtered(db.Orders, filter);
        var needsAction = await InTab(orders, AdminOrderTab.NeedsAction).CountAsync(cancellationToken);
        var shipped = await InTab(orders, AdminOrderTab.Shipped).CountAsync(cancellationToken);
        var cancelled = await InTab(orders, AdminOrderTab.Cancelled).CountAsync(cancellationToken);
        var all = await orders.CountAsync(cancellationToken);
        return new AdminOrderTabCounts(needsAction, shipped, cancelled, all);
    }

    private static IQueryable<Order> Filtered(IQueryable<Order> orders, AdminOrderFilter filter)
    {
        if (filter.PreorderOnly)
        {
            orders = orders.Where(o => o.Lines.Any(l => l.IsPreorder));
        }
        if (filter.ToFulfillOnly)
        {
            orders = orders.Where(AdminQueryFilters.ToFulfill);
        }
        if (filter.UnpaidCashOnly)
        {
            orders = orders.Where(AdminQueryFilters.UnpaidCash);
        }

        var search = filter.Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            return orders;
        }

        // "#42" is unambiguously an order number; a bare "42" could also be part of an email.
        var orderNumberOnly = search.StartsWith('#');
        var term = search.TrimStart('#').Trim();
        int? orderId = int.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        if (orderNumberOnly)
        {
            return orderId is { } exact ? orders.Where(o => o.Id == exact) : orders.Where(o => false);
        }

        // Lower-cased on both sides so the match is case-insensitive on Postgres too.
        var lowered = term.ToLowerInvariant();
        return orders.Where(o =>
            o.Id == orderId
            || o.Email.ToLower().Contains(lowered)
            || o.ShippingAddress.FullName.ToLower().Contains(lowered));
    }

    private static IQueryable<Order> InTab(IQueryable<Order> orders, AdminOrderTab tab) => tab switch
    {
        AdminOrderTab.NeedsAction => orders.Where(AdminQueryFilters.NeedsAction),
        AdminOrderTab.Shipped => orders.Where(o => o.Status == OrderStatus.Shipped || o.Status == OrderStatus.PickedUp),
        AdminOrderTab.Cancelled => orders.Where(o => o.Status == OrderStatus.Cancelled),
        AdminOrderTab.All => orders,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
    };

    public async Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        var order = await LoadWithLinesAsync(id, cancellationToken);

        return order is null ? null : ToDetail(order);
    }

    public async Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken)
    {
        // Loaded up front because the shipping email needs the full order and its lines.
        var order = await LoadWithLinesAsync(id, cancellationToken);
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
            await emailSender.SendShippedNotificationAsync(order.ToShippedView(), cancellationToken);
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

    public async Task<OrderUpdateResult> UpdateInternalNoteAsync(int id, string? note, CancellationToken cancellationToken)
    {
        // Callers validate the normalized length first; this guard only catches a caller that didn't.
        var normalized = OrderNotePolicy.Normalize(note);
        if (normalized?.Length > OrderNotePolicy.MaxLength)
        {
            throw new ArgumentException($"An internal note can be at most {OrderNotePolicy.MaxLength} characters.", nameof(note));
        }

        var rows = await db.Orders
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.InternalNote, normalized), cancellationToken);

        return rows > 0 ? OrderUpdateResult.Updated : OrderUpdateResult.NotFound;
    }

    public Task<EmailResendResult> ResendConfirmationAsync(int id, CancellationToken cancellationToken) =>
        ResendAsync(
            id,
            OrderTransitionPolicy.CanResendConfirmation,
            order => emailSender.SendOrderConfirmationAsync(order.ToConfirmationView(), cancellationToken),
            "order confirmation",
            cancellationToken);

    public Task<EmailResendResult> ResendShippedEmailAsync(int id, CancellationToken cancellationToken) =>
        ResendAsync(
            id,
            OrderTransitionPolicy.CanResendShippedEmail,
            order => emailSender.SendShippedNotificationAsync(order.ToShippedView(), cancellationToken),
            "shipping-notification",
            cancellationToken);

    // Read-only: the order is loaded untracked and nothing is written, so a resend never touches
    // the status or the step timestamps. A failed send is logged and reported, not thrown.
    private async Task<EmailResendResult> ResendAsync(int id, Func<OrderStatus, bool> isAllowed, Func<Order, Task> send, string emailKind, CancellationToken cancellationToken)
    {
        var order = await LoadWithLinesAsync(id, cancellationToken);
        if (order is null)
        {
            return EmailResendResult.NotFound;
        }
        if (!isAllowed(order.Status))
        {
            return EmailResendResult.NotAllowed;
        }

        try
        {
            await send(order);
            return EmailResendResult.Sent;
        }
        // Filtered on the caller's token, not the exception type: an HttpClient timeout is also an
        // OperationCanceledException and must surface as Failed, not escape as a 500.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to resend {EmailKind} email for order {OrderId}", emailKind, order.Id);
            return EmailResendResult.Failed;
        }
    }

    private Task<Order?> LoadWithLinesAsync(int id, CancellationToken cancellationToken) =>
        db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

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
        order.Lines.Select(l => l.ToConfirmationLine()).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.Status,
        order.TrackingNumber,
        order.InternalNote,
        order.CreatedAt,
        order.PaidAt,
        order.ShippedAt,
        order.PickedUpAt,
        order.CancelledAt,
        OrderTransitionPolicy.CanMarkShipped(order.Status),
        OrderTransitionPolicy.CanMarkPickedUp(order.Status, order.FulfillmentMethod),
        OrderTransitionPolicy.CanMarkPaid(order.Status, order.IsPaid),
        OrderTransitionPolicy.CanMarkCancelled(order.Status),
        OrderTransitionPolicy.NeedsRefund(order.Status, order.PaymentMethod, order.IsPaid),
        OrderTransitionPolicy.CanResendConfirmation(order.Status),
        OrderTransitionPolicy.CanResendShippedEmail(order.Status));

    private sealed record OrderState(OrderStatus Status, FulfillmentMethod FulfillmentMethod, bool IsPaid);
}
