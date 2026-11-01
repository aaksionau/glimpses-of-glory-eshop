using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

// Prices the session cart against the catalogue and says which lines the stock can't cover.
// Products are read untracked, so a re-read after a rolled-back reservation sees the database's
// current stock.
public sealed class CheckoutPricer(AppDbContext dbContext, ICartService cartService)
{
    public async Task<CartPricing> PriceAsync(FulfillmentMethod fulfillmentMethod, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return new CartPricing(null, []);
        }

        // Re-derives prices/stock from the DB rather than trusting the session-cached cart,
        // since that's what the shopper is actually about to be charged.
        var products = await LoadBySlugAsync(cart, cancellationToken);
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
        var repricedCart = cart with
        {
            Lines = cart.Lines.Select((l, i) => l with { ProductName = lines[i].ProductName, UnitPrice = lines[i].UnitPrice }).ToList(),
            Subtotal = subtotal,
        };
        return new CartPricing(new PricedCart(lines, repricedCart, fulfillmentMethod), []);
    }

    // The cart lines whose product is gone or can no longer fulfil the requested quantity.
    public async Task<IReadOnlyList<UnavailableCartLine>> FindUnavailableLinesAsync(CartSummary cart, CancellationToken cancellationToken) =>
        FindUnavailableLines(cart, await LoadBySlugAsync(cart, cancellationToken));

    // The in-stock products these lines want more of than is left. Lines for the same product are
    // summed, as reserving them consumes the stock together; preorder lines don't draw on stock.
    public async Task<IReadOnlyList<StockShortfall>> FindShortfallsAsync(IEnumerable<ICheckoutLine> lines, CancellationToken cancellationToken)
    {
        var wanted = lines
            .Where(l => !l.IsPreorder)
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Name: g.First().ProductName, Quantity: g.Sum(l => l.Quantity)))
            .ToList();
        var productIds = wanted.Select(w => w.ProductId).ToList();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return wanted
            .Select(w => (w, left: LeftIfShort(products.GetValueOrDefault(w.ProductId), w.Quantity)))
            .Where(x => x.left is not null)
            .Select(x => new StockShortfall(x.w.ProductId, x.w.Name, x.w.Quantity, x.left!.Value))
            .ToList();
    }

    private static List<UnavailableCartLine> FindUnavailableLines(CartSummary cart, Dictionary<string, Product> products) =>
        cart.Lines
            .Select(line => (line, left: LeftIfShort(products.GetValueOrDefault(line.ProductSlug), line.Quantity)))
            .Where(x => x.left is not null)
            .Select(x => new UnavailableCartLine(x.line.ProductSlug, x.left!.Value))
            .ToList();

    // The one stock rule: null when the product can cover the quantity, otherwise how many are
    // left (0 for a product that's gone).
    private static int? LeftIfShort(Product? product, int quantity) =>
        product is not null && product.CanFulfill(quantity) ? null : Math.Max(0, product?.StockQuantity ?? 0);

    private async Task<Dictionary<string, Product>> LoadBySlugAsync(CartSummary cart, CancellationToken cancellationToken)
    {
        var slugs = cart.Lines.Select(l => l.ProductSlug).ToList();
        return await dbContext.Products
            .AsNoTracking()
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, cancellationToken);
    }
}
