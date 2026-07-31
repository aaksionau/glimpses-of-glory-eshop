using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Inventory;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Abstractions.Payments;
using GlimpsesOfGlory.Abstractions.Shipping;
using GlimpsesOfGlory.Core.Orders.Entities;
using GlimpsesOfGlory.Core.Orders.ValueObjects;
using GlimpsesOfGlory.Core.Shipping.Services;
using GlimpsesOfGlory.Core.Shipping.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Orders.Services;

public sealed class OrderService(
    AppDbContext dbContext,
    ICartService cartService,
    IPaymentGateway paymentGateway,
    IInventoryStore inventoryStore,
    IShippingSettingsService shippingSettingsService) : IOrderService
{
    public async Task<PaymentIntentSetup?> CreatePaymentIntentAsync(ShippingAddressInfo address, CancellationToken cancellationToken)
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

        var tiers = await shippingSettingsService.GetTiersAsync(cancellationToken);
        var shippingCalculator = new ShippingCalculator(
            tiers.Select(t => new ShippingTier(t.MinQuantity, t.Amount)).ToList());

        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var shippingCost = shippingCalculator.Calculate(lines.Sum(l => l.Quantity));
        var total = subtotal + shippingCost;

        var paymentIntent = await paymentGateway.CreatePaymentIntentAsync(total, "usd", address.Email, metadata: null, cancellationToken);

        dbContext.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = paymentIntent.PaymentIntentId,
            Email = address.Email,
            ShippingAddress = ToShippingAddress(address),
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            Total = total,
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

        dbContext.Orders.Add(new Order
        {
            Email = pendingCheckout.Email,
            ShippingAddress = CopyShippingAddress(pendingCheckout.ShippingAddress),
            Subtotal = pendingCheckout.Subtotal,
            ShippingCost = pendingCheckout.ShippingCost,
            Total = pendingCheckout.Total,
            StripePaymentIntentId = pendingCheckout.StripePaymentIntentId,
            Lines = pendingCheckout.Lines.Select(l => new OrderLine
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
            }).ToList(),
        });
        dbContext.PendingCheckouts.Remove(pendingCheckout);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

        return new OrderConfirmationView(
            order.Id,
            new ShippingAddressInfo(
                order.Email,
                order.ShippingAddress.FullName,
                order.ShippingAddress.AddressLine1,
                order.ShippingAddress.AddressLine2,
                order.ShippingAddress.City,
                order.ShippingAddress.State,
                order.ShippingAddress.PostalCode,
                order.ShippingAddress.Country),
            order.Lines.Select(l => new OrderConfirmationLine(l.ProductName, l.UnitPrice, l.Quantity)).ToList(),
            order.Subtotal,
            order.ShippingCost,
            order.Total,
            order.CreatedAt);
    }

    private static ShippingAddress ToShippingAddress(ShippingAddressInfo address) => new()
    {
        FullName = address.FullName,
        AddressLine1 = address.AddressLine1,
        AddressLine2 = address.AddressLine2,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };

    private static ShippingAddress CopyShippingAddress(ShippingAddress address) => new()
    {
        FullName = address.FullName,
        AddressLine1 = address.AddressLine1,
        AddressLine2 = address.AddressLine2,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };
}
