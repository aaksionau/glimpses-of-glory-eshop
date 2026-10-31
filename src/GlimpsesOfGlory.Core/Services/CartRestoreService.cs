using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

public sealed class CartRestoreService(AppDbContext db, IProductCatalogService productCatalogService, ICartStore cartStore) : ICartRestoreService
{
    // Each stored line is matched by product id to the current product, so the cart shows today's name,
    // price and photo rather than the snapshot. Stock is left to the cart and checkout to check as usual.
    public async Task<CartRestoreResult> RestoreAsync(string token, CancellationToken cancellationToken)
    {
        var checkout = await db.PendingCheckouts
            .Where(p => p.RestoreToken == token)
            .Select(p => new
            {
                p.StripePaymentIntentId,
                Lines = p.Lines.Select(l => new { l.ProductId, l.Quantity }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (checkout is null)
        {
            return CartRestoreResult.Expired();
        }

        var products = await productCatalogService.GetProductsByIdsAsync(checkout.Lines.Select(l => l.ProductId).ToList(), cancellationToken);

        var cart = await cartStore.GetCartAsync(cancellationToken);
        var skipped = 0;
        foreach (var line in checkout.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                skipped++;
                continue;
            }

            // The larger quantity wins, so following the link twice changes nothing.
            var quantity = Math.Max(cart.QuantityOf(product.Slug), line.Quantity);
            cart.SetLineQuantity(product.Slug, product.Name, product.Price, product.ThumbnailFileName, quantity, product.IsPreorder, product.ExpectedAvailabilityDate);
        }

        await cartStore.SaveCartAsync(cart, cancellationToken);
        return CartRestoreResult.Restored(checkout.StripePaymentIntentId, skipped);
    }
}
