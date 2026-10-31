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
    OrderLineInventory orderLineInventory,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken)
    {
        var pricing = await PriceCartAsync(fulfillmentMethod, cancellationToken);
        if (pricing.Priced is not { } priced)
        {
            return CheckoutPaymentResult.Failed(pricing.UnavailableLines);
        }

        var existingPendingCheckout = existingPaymentIntentId is null
            ? null
            : await dbContext.PendingCheckouts
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == existingPaymentIntentId, cancellationToken);

        if (existingPendingCheckout is not null)
        {
            try
            {
                var updatedPaymentIntent = await paymentGateway.UpdatePaymentIntentAsync(existingPaymentIntentId!, priced.Total, address.Email, cancellationToken);

                existingPendingCheckout.Email = address.Email;
                existingPendingCheckout.FulfillmentMethod = fulfillmentMethod;
                existingPendingCheckout.ShippingAddress = ShippingAddress.FromInfo(address);
                existingPendingCheckout.Subtotal = priced.Subtotal;
                existingPendingCheckout.ShippingCost = priced.ShippingCost;
                dbContext.PendingCheckoutLines.RemoveRange(existingPendingCheckout.Lines);
                existingPendingCheckout.Lines = priced.Lines;

                await dbContext.SaveChangesAsync(cancellationToken);
                return CheckoutPaymentResult.Ready(new CheckoutPaymentSetup(updatedPaymentIntent.PaymentIntentId, updatedPaymentIntent.ClientSecret, priced.Cart));
            }
            catch (PaymentIntentUnavailableException)
            {
                // The previous PaymentIntent can no longer be updated (already paid, canceled,
                // or expired) - fall through and start a fresh one below.
                dbContext.PendingCheckouts.Remove(existingPendingCheckout);
            }
        }

        var paymentIntent = await paymentGateway.CreatePaymentIntentAsync(priced.Total, address.Email, cancellationToken);

        dbContext.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = paymentIntent.PaymentIntentId,
            Email = address.Email,
            FulfillmentMethod = fulfillmentMethod,
            ShippingAddress = ShippingAddress.FromInfo(address),
            Subtotal = priced.Subtotal,
            ShippingCost = priced.ShippingCost,
            Lines = priced.Lines,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return CheckoutPaymentResult.Ready(new CheckoutPaymentSetup(paymentIntent.PaymentIntentId, paymentIntent.ClientSecret, priced.Cart));
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
            // changed between PaymentIntent creation and webhook delivery. The order is
            // intentionally left uncreated here; this edge case needs manual follow-up (refund).
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
        var pricing = await PriceCartAsync(FulfillmentMethod.Pickup, cancellationToken);
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
            var products = await LoadProductsAsync(priced.Cart, cancellationToken);
            return CashOrderResult.Failed(FindUnavailableLines(priced.Cart, products));
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

    // Priced is null when the cart is empty or can't be fulfilled as it stands; UnavailableLines
    // then lists the lines that are short (none for an empty cart). Priced.Cart is the session
    // cart with its lines repriced from the catalogue, for showing what's charged.
    private async Task<CartPricing> PriceCartAsync(FulfillmentMethod fulfillmentMethod, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return new CartPricing(null, []);
        }

        // Re-derives prices/stock from the DB rather than trusting the session-cached cart,
        // since that's what the shopper is actually about to be charged.
        var products = await LoadProductsAsync(cart, cancellationToken);
        var unavailableLines = FindUnavailableLines(cart, products);
        if (unavailableLines.Count > 0)
        {
            return new CartPricing(null, unavailableLines);
        }

        var lines = cart.Lines.Select(line =>
        {
            var product = products[line.ProductSlug];
            return new PendingCheckoutLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
                IsPreorder = product.IsPreorder,
                ExpectedAvailabilityDate = product.ExpectedAvailabilityDate,
            };
        }).ToList();

        // Shipping only depends on item quantity (and fulfillment method), which the
        // session-cached cart already has correct - only prices/stock need re-deriving
        // from the DB above.
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        // The lines keep the cart's line order, so they pair up by index.
        var pricedCart = cart with
        {
            Lines = cart.Lines.Select((l, i) => l with { ProductName = lines[i].ProductName, UnitPrice = lines[i].UnitPrice }).ToList(),
            Subtotal = subtotal,
        };
        return new CartPricing(new PricedCart(lines, subtotal, cart.ShippingCostFor(fulfillmentMethod), pricedCart), []);
    }

    // Untracked, so a re-read after a rolled-back reservation sees the database's current stock.
    private async Task<Dictionary<string, Product>> LoadProductsAsync(CartSummary cart, CancellationToken cancellationToken)
    {
        var slugs = cart.Lines.Select(l => l.ProductSlug).ToList();
        return await dbContext.Products
            .AsNoTracking()
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, cancellationToken);
    }

    // The cart lines whose product is gone or can no longer fulfil the requested quantity.
    private static List<UnavailableCartLine> FindUnavailableLines(CartSummary cart, Dictionary<string, Product> products) =>
        cart.Lines
            .Select(line => (line, product: products.GetValueOrDefault(line.ProductSlug)))
            .Where(x => x.product is null || !x.product.CanFulfill(x.line.Quantity))
            .Select(x => new UnavailableCartLine(x.line.ProductSlug, Math.Max(0, x.product?.StockQuantity ?? 0)))
            .ToList();

    private sealed record CartPricing(PricedCart? Priced, IReadOnlyList<UnavailableCartLine> UnavailableLines);

    private sealed record PricedCart(List<PendingCheckoutLine> Lines, decimal Subtotal, decimal ShippingCost, CartSummary Cart)
    {
        public decimal Total => Subtotal + ShippingCost;
    }

    // The order is already committed and stock already decremented - a failed confirmation
    // email shouldn't undo it. Log for manual follow-up instead.
    private Task SendConfirmationEmailAsync(Order order, CancellationToken cancellationToken) =>
        BestEffortEmail.TrySendAsync(
            () => emailSender.SendOrderConfirmationAsync(order.ToConfirmationView(), cancellationToken),
            ex => logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}", order.Id),
            cancellationToken);

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
