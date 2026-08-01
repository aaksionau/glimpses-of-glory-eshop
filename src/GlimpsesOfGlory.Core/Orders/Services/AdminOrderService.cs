using GlimpsesOfGlory.Abstractions.Notifications;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Core.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.Core.Orders.Services;

public sealed class AdminOrderService(AppDbContext db, IEmailSender emailSender, ILogger<AdminOrderService> logger) : IAdminOrderService
{
    public async Task<IReadOnlyList<AdminOrderSummary>> GetAllOrdersAsync(CancellationToken cancellationToken)
    {
        return await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderSummary(
                o.Id,
                o.Email,
                o.Lines.Sum(l => l.Quantity),
                o.Total,
                o.Status,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : ToDetail(order);
    }

    public async Task<bool> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return false;
        }

        order.Status = OrderStatus.Shipped;
        order.TrackingNumber = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber;
        await db.SaveChangesAsync(cancellationToken);

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

        return true;
    }

    public async Task<bool> MarkCancelledAsync(int id, CancellationToken cancellationToken)
    {
        var rows = await db.Orders
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, OrderStatus.Cancelled), cancellationToken);

        return rows > 0;
    }

    private static AdminOrderDetail ToDetail(Order order) => new(
        order.Id,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity)).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.Status,
        order.TrackingNumber,
        order.CreatedAt);

    private static OrderShippedView ToShippedView(Order order) => new(
        order.Id,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity)).ToList(),
        order.Total,
        order.TrackingNumber);
}
