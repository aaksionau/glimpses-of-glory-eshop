using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Core.Services;

public sealed class CartRestoreService(AppDbContext db, ICartStore cartStore) : ICartRestoreService
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

        var productIds = checkout.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Name,
                p.Price,
                ThumbnailFileName = p.Photos.OrderBy(photo => photo.DisplayOrder).Select(photo => photo.FileName).FirstOrDefault(),
                p.IsPreorder,
                p.ExpectedAvailabilityDate,
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

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
            var currentQuantity = cart.Lines.FirstOrDefault(l => l.ProductSlug == product.Slug)?.Quantity ?? 0;
            cart.SetLineQuantity(product.Slug, product.Name, product.Price, product.ThumbnailFileName, Math.Max(currentQuantity, line.Quantity), product.IsPreorder, product.ExpectedAvailabilityDate);
        }

        await cartStore.SaveCartAsync(cart, cancellationToken);
        return CartRestoreResult.Restored(checkout.StripePaymentIntentId, skipped);
    }
}
