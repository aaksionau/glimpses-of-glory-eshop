using System.Globalization;
using System.Runtime.CompilerServices;
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

public sealed class AdminOrderService(AppDbContext db, OrderLineInventory orderLineInventory, IPaymentGateway paymentGateway, IEmailSender emailSender, TimeProvider timeProvider, ILogger<AdminOrderService> logger) : IAdminOrderService
{
    public async Task<AdminOrderPage> GetOrdersAsync(AdminOrderTab tab, AdminOrderFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var orders = Filtered(InTab(db.Orders.AsNoTracking(), tab), filter);
        var totalCount = await orders.CountAsync(cancellationToken);
        var lastPage = AdminOrderPage.PageCount(totalCount, pageSize);
        page = Math.Clamp(page, 1, lastPage);

        // Id breaks CreatedAt ties so an order can't appear on two pages, or on none.
        var rows = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                Summary = new AdminOrderSummary(
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
                    false,
                    false,
                    o.InternalNote != null,
                    o.CreatedAt),
                o.PaymentMethod,
            })
            .ToListAsync(cancellationToken);

        // The lifecycle flags are filled in from the page's rows, so the rules run in memory only.
        var items = rows.Select(r => WithLifecycleFlags(r.Summary, r.PaymentMethod)).ToList();

        return new AdminOrderPage(items, page, pageSize, totalCount);
    }

    private static AdminOrderSummary WithLifecycleFlags(AdminOrderSummary summary, PaymentMethod paymentMethod)
    {
        var facts = new OrderFacts
        {
            Status = summary.Status,
            FulfillmentMethod = summary.FulfillmentMethod,
            PaymentMethod = paymentMethod,
            IsPaid = summary.IsPaid,
        };
        return summary with
        {
            NeedsRefund = OrderLifecycle.NeedsRefund.IsSatisfiedBy(facts),
            NeedsAction = OrderLifecycle.NeedsAction.IsSatisfiedBy(facts),
        };
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
            orders = orders.Where(OrderLifecycle.ToFulfill.OnOrders);
        }
        if (filter.UnpaidCashOnly)
        {
            orders = orders.Where(OrderLifecycle.UnpaidCash.OnOrders);
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
        AdminOrderTab.NeedsAction => orders.Where(OrderLifecycle.NeedsAction.OnOrders),
        AdminOrderTab.Shipped => orders.Where(OrderLifecycle.ShippedOrCollected.OnOrders),
        AdminOrderTab.Cancelled => orders.Where(OrderLifecycle.Cancelled.OnOrders),
        AdminOrderTab.All => orders,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
    };

    public IAsyncEnumerable<AdminOrderExportRow> StreamOrdersForExportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("The export range must not end before it starts.", nameof(to));
        }

        // Half-open [from, day after to) in UTC, so every moment of the last day is included.
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return StreamOrdersCreatedBetweenAsync(start, end, cancellationToken);
    }

    // An async iterator, so the token is honored even if the caller never calls WithCancellation;
    // kept apart from the public method so a backwards range still throws eagerly.
    private async IAsyncEnumerable<AdminOrderExportRow> StreamOrdersCreatedBetweenAsync(
        DateTimeOffset start, DateTimeOffset end, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Select(o => new AdminOrderExportRow(
                o.Id,
                o.CreatedAt,
                o.ShippingAddress.FullName,
                o.Email,
                o.FulfillmentMethod,
                o.Status,
                o.PaymentMethod,
                o.PaidAt,
                o.Subtotal,
                o.ShippingCost))
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        await foreach (var row in rows)
        {
            yield return row;
        }
    }

    public async Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        var order = await LoadWithLinesAsync(id, cancellationToken);

        return order is null ? null : ToDetail(order);
    }

    public async Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, ShippingCarrier carrier, CancellationToken cancellationToken)
    {
        // The page validates the posted carrier; this keeps an undefined value from ever being stored.
        if (!Enum.IsDefined(carrier))
        {
            throw new ArgumentOutOfRangeException(nameof(carrier), carrier, null);
        }

        // Guarded on the status the transition is checked against, so a double-click (or two
        // admins at once) only ships - and emails the buyer - once.
        var tracking = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim();
        // A carrier without a tracking number has nothing to link to, so it isn't recorded.
        ShippingCarrier? shippedWith = tracking is null ? null : carrier;
        var result = await TransitionAsync(id, OrderLifecycle.MarkShipped, cancellationToken, setters => setters
            .SetProperty(o => o.TrackingNumber, tracking)
            .SetProperty(o => o.Carrier, shippedWith));
        if (result != OrderUpdateResult.Updated)
        {
            return result;
        }

        // Loaded after the update, so the email shows exactly what was committed.
        var order = await LoadWithLinesAsync(id, cancellationToken);
        if (order is null)
        {
            return OrderUpdateResult.Updated;
        }
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

    public Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, OrderLifecycle.MarkPickedUp, cancellationToken);

    // The payment date is coalesced like every step timestamp, so a concurrent repeat click doesn't move it.
    public Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, OrderLifecycle.MarkPaid, cancellationToken);

    public async Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        // The status guard and the restock commit together, so a concurrent repeat click that
        // loses the guard can't restock a second time, and a failed restock leaves the order New.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await TransitionAsync(id, OrderLifecycle.MarkCancelled, cancellationToken);
        if (result != OrderUpdateResult.Updated)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        var lines = await db.OrderLines
            .AsNoTracking()
            .Where(l => l.OrderId == id)
            .ToListAsync(cancellationToken);
        await orderLineInventory.ReleaseAsync(lines, cancellationToken);

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
            OrderLifecycle.CanResendConfirmation,
            order => emailSender.SendOrderConfirmationAsync(order.ToConfirmationView(), cancellationToken),
            "order confirmation",
            cancellationToken);

    public Task<EmailResendResult> ResendShippedEmailAsync(int id, CancellationToken cancellationToken) =>
        ResendAsync(
            id,
            OrderLifecycle.CanResendShippedEmail,
            order => emailSender.SendShippedNotificationAsync(order.ToShippedView(), cancellationToken),
            "shipping-notification",
            cancellationToken);

    // Read-only: the order is loaded untracked and nothing is written, so a resend never touches
    // the status or the step timestamps. A failed send is logged and reported, not thrown.
    private async Task<EmailResendResult> ResendAsync(int id, OrderRule isAllowed, Func<Order, Task> send, string emailKind, CancellationToken cancellationToken)
    {
        var order = await LoadWithLinesAsync(id, cancellationToken);
        if (order is null)
        {
            return EmailResendResult.NotFound;
        }
        if (!isAllowed.IsSatisfiedBy(OrderFacts.Of(order)))
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

    // Checks the transition against the order's current facts, then applies it only if the order
    // is still in that status, so a concurrent change (another tab, another admin) can't be
    // overridden. alsoSet adds the action's own columns to the same guarded update.
    private async Task<OrderUpdateResult> TransitionAsync(int id, OrderTransition transition, CancellationToken cancellationToken, Action<UpdateSettersBuilder<Order>>? alsoSet = null)
    {
        var facts = await db.Orders
            .Where(o => o.Id == id)
            .Select(OrderFacts.FromOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (facts is null)
        {
            return OrderUpdateResult.NotFound;
        }
        if (!transition.IsAllowed(facts))
        {
            return OrderUpdateResult.InvalidTransition;
        }

        var now = timeProvider.GetUtcNow();
        var rows = await db.Orders
            .Where(o => o.Id == id && o.Status == facts.Status)
            .ExecuteUpdateAsync(setters =>
            {
                transition.Apply(setters, now);
                alsoSet?.Invoke(setters);
            }, cancellationToken);

        return rows > 0 ? OrderUpdateResult.Updated : OrderUpdateResult.InvalidTransition;
    }

    private AdminOrderDetail ToDetail(Order order)
    {
        var facts = OrderFacts.Of(order);
        return new(
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
            order.Carrier,
            CarrierTrackingLinks.TrackingUrl(order.Carrier, order.TrackingNumber),
            order.InternalNote,
            order.CreatedAt,
            order.PaidAt,
            order.ShippedAt,
            order.PickedUpAt,
            order.CancelledAt,
            OrderLifecycle.MarkShipped.IsAllowed(facts),
            OrderLifecycle.MarkPickedUp.IsAllowed(facts),
            OrderLifecycle.MarkPaid.IsAllowed(facts),
            OrderLifecycle.MarkCancelled.IsAllowed(facts),
            OrderLifecycle.NeedsRefund.IsSatisfiedBy(facts),
            OrderLifecycle.CanResendConfirmation.IsSatisfiedBy(facts),
            OrderLifecycle.CanResendShippedEmail.IsSatisfiedBy(facts));
    }
}
