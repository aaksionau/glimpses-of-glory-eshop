using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Exceptions;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.Extensions;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace GlimpsesOfGlory.Core.Services;

public sealed class OrderService(
    AppDbContext dbContext,
    ICartService cartService,
    IPaymentGateway paymentGateway,
    IInventoryStore inventoryStore,
    IEmailSender emailSender,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<PaymentIntentSetup?> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken)
    {
        if (await PriceCartAsync(fulfillmentMethod, cancellationToken) is not (var lines, var subtotal, var shippingCost))
        {
            return null;
        }

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
                existingPendingCheckout.FulfillmentMethod = fulfillmentMethod;
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
            FulfillmentMethod = fulfillmentMethod,
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

        if (!await TryReserveStockAsync(pendingCheckout.Lines, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            // Payment succeeded but stock ran out before confirmation - only possible if stock
            // changed between PaymentIntent creation and webhook delivery. The order is
            // intentionally left uncreated here; this edge case needs manual follow-up (refund).
            return;
        }

        var order = new Order
        {
            Email = pendingCheckout.Email,
            FulfillmentMethod = pendingCheckout.FulfillmentMethod,
            ShippingAddress = pendingCheckout.ShippingAddress.Clone(),
            Subtotal = pendingCheckout.Subtotal,
            ShippingCost = pendingCheckout.ShippingCost,
            PaymentMethod = PaymentMethod.Card,
            PaidAt = DateTimeOffset.UtcNow,
            StripePaymentIntentId = pendingCheckout.StripePaymentIntentId,
            Lines = pendingCheckout.Lines.Select(ToOrderLine).ToList(),
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

        // A failed email mustn't fail the webhook: Stripe would retry, but the idempotency
        // check above means the retry would just no-op and never resend the email anyway.
        await SendConfirmationEmailAsync(order, cancellationToken);
    }

    public async Task<int?> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken)
    {
        // Cash is only offered for local pickup.
        if (await PriceCartAsync(FulfillmentMethod.Pickup, cancellationToken) is not (var lines, var subtotal, var shippingCost))
        {
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!await TryReserveStockAsync(lines, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var order = new Order
        {
            Email = address.Email,
            FulfillmentMethod = FulfillmentMethod.Pickup,
            ShippingAddress = ShippingAddress.FromInfo(address),
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            PaymentMethod = PaymentMethod.Cash,
            Lines = lines.Select(ToOrderLine).ToList(),
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await SendConfirmationEmailAsync(order, cancellationToken);
        return order.Id;
    }

    public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        GetOrderConfirmationAsync(o => o.StripePaymentIntentId == paymentIntentId, cancellationToken);

    public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) =>
        GetOrderConfirmationAsync(o => o.Id == orderId, cancellationToken);

    private async Task<OrderConfirmationView?> GetOrderConfirmationAsync(Expression<Func<Order, bool>> predicate, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(predicate, cancellationToken);

        return order is null ? null : ToConfirmationView(order);
    }

    // Returns null when the cart is empty or can't be fulfilled as it stands.
    private async Task<(List<PendingCheckoutLine> Lines, decimal Subtotal, decimal ShippingCost)?> PriceCartAsync(FulfillmentMethod fulfillmentMethod, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return null;
        }

        var lines = await BuildCheckoutLinesAsync(cart, cancellationToken);
        if (lines is null)
        {
            return null;
        }

        // Shipping only depends on item quantity (and fulfillment method), which the
        // session-cached cart already has correct - only prices/stock need re-deriving
        // from the DB above.
        var shippingCost = await cartService.GetShippingCostAsync(fulfillmentMethod, cancellationToken);
        return (lines, lines.Sum(l => l.UnitPrice * l.Quantity), shippingCost);
    }

    // Re-derives prices/stock from the DB rather than trusting the session-cached cart,
    // since that's what the shopper is actually about to be charged. Returns null when a
    // product is gone or can no longer fulfil the requested quantity.
    private async Task<List<PendingCheckoutLine>?> BuildCheckoutLinesAsync(CartSummary cart, CancellationToken cancellationToken)
    {
        var slugs = cart.Lines.Select(l => l.ProductSlug).ToList();
        var products = await dbContext.Products
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, cancellationToken);

        var lines = new List<PendingCheckoutLine>();
        foreach (var line in cart.Lines)
        {
            if (!products.TryGetValue(line.ProductSlug, out var product) || !product.CanFulfill(line.Quantity))
            {
                return null;
            }

            lines.Add(new PendingCheckoutLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
                IsPreorder = product.IsPreorder,
                ExpectedAvailabilityDate = product.ExpectedAvailabilityDate,
            });
        }

        return lines;
    }

    // Must run inside a transaction - on false the caller rolls back so the lines
    // already reserved don't stand.
    private async Task<bool> TryReserveStockAsync(IEnumerable<ICheckoutLine> lines, CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (line.IsPreorder)
            {
                await inventoryStore.ReservePreorderAsync(line.ProductId, line.Quantity, cancellationToken);
                continue;
            }

            if (!await inventoryStore.TryReserveStockAsync(line.ProductId, line.Quantity, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task SendConfirmationEmailAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendOrderConfirmationAsync(ToConfirmationView(order), cancellationToken);
        }
        catch (Exception ex)
        {
            // The order is already committed and stock already decremented - a failed
            // confirmation email shouldn't undo it. Log for manual follow-up instead.
            logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}", order.Id);
        }
    }

    private static OrderLine ToOrderLine(ICheckoutLine line) => new()
    {
        ProductId = line.ProductId,
        ProductName = line.ProductName,
        UnitPrice = line.UnitPrice,
        Quantity = line.Quantity,
        IsPreorder = line.IsPreorder,
        ExpectedAvailabilityDate = line.ExpectedAvailabilityDate,
    };

    private static OrderConfirmationView ToConfirmationView(Order order) => new(
        order.Id,
        order.FulfillmentMethod,
        order.IsPaid,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity, l.IsPreorder, l.ExpectedAvailabilityDate)).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.CreatedAt);
}
