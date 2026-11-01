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

public sealed partial class OrderService(
    AppDbContext dbContext,
    CheckoutPricer checkoutPricer,
    IPaymentGateway paymentGateway,
    OrderLineInventory orderLineInventory,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken)
    {
        var pricing = await checkoutPricer.PriceAsync(fulfillmentMethod, cancellationToken);
        if (pricing.Priced is not { } priced)
        {
            return CheckoutPaymentResult.Failed(pricing.UnavailableLines);
        }

        var pendingCheckout = existingPaymentIntentId is null
            ? null
            : await dbContext.PendingCheckouts
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == existingPaymentIntentId, cancellationToken);
        var paymentIntent = pendingCheckout is null
            ? null
            : await TryUpdatePaymentIntentAsync(pendingCheckout.StripePaymentIntentId, priced, address.Email, cancellationToken);

        if (paymentIntent is null || pendingCheckout is null)
        {
            // Nothing to reuse: drop the stale checkout, if any, and start a fresh PaymentIntent.
            if (pendingCheckout is not null)
            {
                dbContext.PendingCheckouts.Remove(pendingCheckout);
            }
            paymentIntent = await paymentGateway.CreatePaymentIntentAsync(priced.Total, address.Email, cancellationToken);
            pendingCheckout = new PendingCheckout
            {
                StripePaymentIntentId = paymentIntent.PaymentIntentId,
                Email = address.Email,
                ShippingAddress = ShippingAddress.FromInfo(address),
            };
            dbContext.PendingCheckouts.Add(pendingCheckout);
        }

        SnapshotCheckout(pendingCheckout, priced, address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CheckoutPaymentResult.Ready(new CheckoutPaymentSetup(paymentIntent.PaymentIntentId, paymentIntent.ClientSecret, priced.Cart));
    }

    // Null when the previous PaymentIntent can no longer be updated (already paid, canceled, or
    // expired), so the caller starts a fresh one.
    private async Task<PaymentIntentSetup?> TryUpdatePaymentIntentAsync(string paymentIntentId, PricedCart priced, string email, CancellationToken cancellationToken)
    {
        try
        {
            return await paymentGateway.UpdatePaymentIntentAsync(paymentIntentId, priced.Total, email, cancellationToken);
        }
        catch (PaymentIntentUnavailableException)
        {
            return null;
        }
    }

    // Records what the webhook will turn into an order, replacing any earlier snapshot's lines.
    private void SnapshotCheckout(PendingCheckout pendingCheckout, PricedCart priced, ShippingAddressInfo address)
    {
        pendingCheckout.Email = address.Email;
        pendingCheckout.FulfillmentMethod = priced.FulfillmentMethod;
        pendingCheckout.ShippingAddress = ShippingAddress.FromInfo(address);
        pendingCheckout.Subtotal = priced.Subtotal;
        pendingCheckout.ShippingCost = priced.ShippingCost;
        dbContext.PendingCheckoutLines.RemoveRange(pendingCheckout.Lines);
        pendingCheckout.Lines = priced.Lines;
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

        if (!await orderLineInventory.TryReserveAsync(pendingCheckout.Lines, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            // Payment succeeded but stock ran out before confirmation - only possible if stock
            // changed between PaymentIntent creation and webhook delivery. The PendingCheckout is kept
            // for the refund, and returning normally keeps the webhook at 200 so Stripe doesn't retry.
            var (shortLines, rereadFailure) = await DescribeShortLinesAsync(pendingCheckout.Lines, cancellationToken);
            LogStockRanOutAfterPayment(logger, rereadFailure, paymentIntentId, pendingCheckout.Email, shortLines);
            return;
        }

        // One clock reading, so a card order's Placed and Paid steps share the same instant.
        var now = timeProvider.GetUtcNow();
        var order = new Order
        {
            Email = pendingCheckout.Email,
            FulfillmentMethod = pendingCheckout.FulfillmentMethod,
            ShippingAddress = pendingCheckout.ShippingAddress.Clone(),
            Subtotal = pendingCheckout.Subtotal,
            ShippingCost = pendingCheckout.ShippingCost,
            PaymentMethod = PaymentMethod.Card,
            CreatedAt = now,
            PaidAt = now,
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

    public async Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken)
    {
        // Cash is only offered for local pickup.
        var pricing = await checkoutPricer.PriceAsync(FulfillmentMethod.Pickup, cancellationToken);
        if (pricing.Priced is not { } priced)
        {
            return CashOrderResult.Failed(pricing.UnavailableLines);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!await orderLineInventory.TryReserveAsync(priced.Lines, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            // Another order took the stock between pricing and reserving: re-read it (outside the
            // rolled-back transaction) to say which lines are now short. If stock came back in the
            // meantime the list is empty and the cart falls back to the generic banner.
            return CashOrderResult.Failed(await checkoutPricer.FindUnavailableLinesAsync(priced.Cart, cancellationToken));
        }

        var order = new Order
        {
            Email = address.Email,
            FulfillmentMethod = FulfillmentMethod.Pickup,
            ShippingAddress = ShippingAddress.FromInfo(address),
            Subtotal = priced.Subtotal,
            ShippingCost = priced.ShippingCost,
            PaymentMethod = PaymentMethod.Cash,
            CreatedAt = timeProvider.GetUtcNow(),
            Lines = priced.Lines.Select(ToOrderLine).ToList(),
        };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await SendConfirmationEmailAsync(order, cancellationToken);
        return CashOrderResult.Placed(order.Id);
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

        return order is null ? null : order.ToConfirmationView();
    }

    // Says which in-stock lines of a paid checkout couldn't be reserved, e.g. "Sunrise (product 3):
    // wanted 2, 1 left". Never throws: a failed stock re-read is returned so the paid-but-orderless
    // error is still logged and the webhook stays at 200.
    private async Task<(string ShortLines, Exception? RereadFailure)> DescribeShortLinesAsync(
        IEnumerable<PendingCheckoutLine> lines, CancellationToken cancellationToken)
    {
        try
        {
            var shortfalls = await checkoutPricer.FindShortfallsAsync(lines, cancellationToken);

            // Empty if stock came back between the failed reservation and this re-read.
            return shortfalls.Count == 0
                ? ("none short on re-read", null)
                : (string.Join("; ", shortfalls.Select(s => $"{s.ProductName} (product {s.ProductId}): wanted {s.Wanted}, {s.Left} left")), null);
        }
        catch (Exception ex)
        {
            return ("unknown (stock re-read failed)", ex);
        }
    }

    // Sent after the order and stock decrement are committed.
    private async Task SendConfirmationEmailAsync(Order order, CancellationToken cancellationToken) =>
        await BestEffortEmail.TrySendAsync(
            () => emailSender.SendOrderConfirmationAsync(order.ToConfirmationView(), cancellationToken),
            ex => LogConfirmationEmailFailed(logger, ex, order.Id),
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment {PaymentIntentId} from {Email} succeeded but no order was created because stock ran out: {ShortLines}. Manual follow-up (refund) needed")]
    private static partial void LogStockRanOutAfterPayment(ILogger logger, Exception? exception, string paymentIntentId, string email, string shortLines);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send order confirmation email for order {OrderId}")]
    private static partial void LogConfirmationEmailFailed(ILogger logger, Exception exception, int orderId);

    private static OrderLine ToOrderLine(ICheckoutLine line) => new()
    {
        ProductId = line.ProductId,
        ProductName = line.ProductName,
        UnitPrice = line.UnitPrice,
        Quantity = line.Quantity,
        IsPreorder = line.IsPreorder,
        ExpectedAvailabilityDate = line.ExpectedAvailabilityDate,
    };
}
