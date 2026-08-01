using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Inventory;
using GlimpsesOfGlory.Abstractions.Notifications;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Abstractions.Payments;
using GlimpsesOfGlory.Core.Orders.Entities;
using GlimpsesOfGlory.Core.Orders.Persistence;
using GlimpsesOfGlory.Core.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.Core.Orders.Services;

public sealed class OrderService(
    AppDbContext dbContext,
    ICartService cartService,
    IPaymentGateway paymentGateway,
    IInventoryStore inventoryStore,
    IEmailSender emailSender,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<PaymentIntentSetup?> CreatePaymentIntentAsync(ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return null;
        }

        var slugs = cart.Lines.Select(l => l.ProductSlug).ToList();
        var products = await dbContext.Products
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, cancellationToken);

        // Re-derive prices/stock from the DB rather than trusting the session-cached
        // cart, since that's what the shopper is actually about to be charged.
        var lines = new List<PendingCheckoutLine>();
        foreach (var line in cart.Lines)
        {
            if (!products.TryGetValue(line.ProductSlug, out var product) || product.StockQuantity < line.Quantity)
            {
                return null;
            }

            lines.Add(new PendingCheckoutLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
            });
        }

        // Shipping only depends on item quantity, which the session-cached cart already
        // has correct (only prices/stock need re-deriving from the DB above), so reuse
        // cart.ShippingCost instead of re-fetching tiers and recalculating it here.
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var shippingCost = cart.ShippingCost;
        var total = subtotal + shippingCost;

        var existingPendingCheckout = existingPaymentIntentId is null
            ? null
            : await dbContext.PendingCheckouts
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == existingPaymentIntentId, cancellationToken);

        if (existingPendingCheckout is not null)
        {
            try
            {
                var updatedPaymentIntent = await paymentGateway.UpdatePaymentIntentAsync(existingPaymentIntentId!, total, address.Email, cancellationToken);

                existingPendingCheckout.Email = address.Email;
                existingPendingCheckout.ShippingAddress = ShippingAddress.FromInfo(address);
                existingPendingCheckout.Subtotal = subtotal;
                existingPendingCheckout.ShippingCost = shippingCost;
                dbContext.PendingCheckoutLines.RemoveRange(existingPendingCheckout.Lines);
                existingPendingCheckout.Lines = lines;

                await dbContext.SaveChangesAsync(cancellationToken);
                return updatedPaymentIntent;
            }
            catch (PaymentIntentUnavailableException)
            {
                // The previous PaymentIntent can no longer be updated (already paid, canceled,
                // or expired) - fall through and start a fresh one below.
                dbContext.PendingCheckouts.Remove(existingPendingCheckout);
            }
        }

        var paymentIntent = await paymentGateway.CreatePaymentIntentAsync(total, address.Email, cancellationToken);

        dbContext.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = paymentIntent.PaymentIntentId,
            Email = address.Email,
            ShippingAddress = ShippingAddress.FromInfo(address),
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            Lines = lines,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return paymentIntent;
    }

    public async Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        var alreadyConfirmed = await dbContext.Orders.AnyAsync(o => o.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (alreadyConfirmed)
        {
            return;
        }

        var pendingCheckout = await dbContext.PendingCheckouts
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (pendingCheckout is null)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var line in pendingCheckout.Lines)
        {
            if (!await inventoryStore.TryReserveStockAsync(line.ProductId, line.Quantity, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                // Payment succeeded but stock ran out before confirmation - only possible if stock
                // changed between PaymentIntent creation and webhook delivery. The order is
                // intentionally left uncreated here; this edge case needs manual follow-up (refund).
                return;
            }
        }

        var order = new Order
        {
            Email = pendingCheckout.Email,
            ShippingAddress = pendingCheckout.ShippingAddress.Clone(),
            Subtotal = pendingCheckout.Subtotal,
            ShippingCost = pendingCheckout.ShippingCost,
            StripePaymentIntentId = pendingCheckout.StripePaymentIntentId,
            Lines = pendingCheckout.Lines.Select(l => new OrderLine
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
            }).ToList(),
        };
        dbContext.Orders.Add(order);
        dbContext.PendingCheckouts.Remove(pendingCheckout);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateStripePaymentIntentId())
        {
            // Lost the race to a concurrent webhook delivery for the same PaymentIntent - roll
            // back this attempt's stock reservation (the winner's decrement is the only one
            // that should stand) and treat it as the idempotent no-op it is.
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await transaction.CommitAsync(cancellationToken);

        var confirmation = ToConfirmationView(order);

        try
        {
            await emailSender.SendOrderConfirmationAsync(confirmation, cancellationToken);
        }
        catch (Exception ex)
        {
            // The order is already committed and stock already decremented - a failed
            // confirmation email shouldn't fail the webhook (Stripe would retry, but the
            // idempotency check above means the retry would just no-op and never resend
            // the email anyway). Log for manual follow-up instead.
            logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}", order.Id);
        }
    }

    public async Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        return ToConfirmationView(order);
    }

    private static OrderConfirmationView ToConfirmationView(Order order) => new(
        order.Id,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity)).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.CreatedAt);
}
