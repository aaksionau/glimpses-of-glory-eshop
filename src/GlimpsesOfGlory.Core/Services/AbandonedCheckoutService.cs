using System.Buffers.Text;
using System.Linq.Expressions;
using System.Security.Cryptography;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlimpsesOfGlory.Core.Services;

public sealed class AbandonedCheckoutService(AppDbContext db, IEmailSender emailSender, TimeProvider timeProvider, ILogger<AbandonedCheckoutService> logger) : IAbandonedCheckoutService
{
    // Younger checkouts may still be mid-payment; older ones are too stale to chase.
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(30);

    private const int RestoreTokenBytes = 32;

    private static readonly Expression<Func<PendingCheckout, AbandonedCheckout>> ToAbandonedCheckout = p => new AbandonedCheckout(
        p.Id,
        p.Email,
        p.Lines.OrderBy(l => l.Id).Select(l => new AbandonedCheckoutLine(l.ProductName, l.Quantity)).ToList(),
        p.Total,
        p.FulfillmentMethod,
        p.CreatedAt,
        p.RemindedAt);

    public async Task<IReadOnlyList<AbandonedCheckout>> GetAbandonedCheckoutsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var createdBefore = now - MinimumAge;
        var createdAfter = now - MaximumAge;
        return await db.PendingCheckouts
            .Where(p => p.CreatedAt < createdBefore && p.CreatedAt > createdAfter)
            .Where(p => !db.Orders.Any(o => o.Email == p.Email && o.CreatedAt > p.CreatedAt))
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToAbandonedCheckout)
            .ToListAsync(cancellationToken);
    }

    public Task<AbandonedCheckout?> GetAbandonedCheckoutAsync(int id, CancellationToken cancellationToken) =>
        db.PendingCheckouts
            .Where(p => p.Id == id)
            .Select(ToAbandonedCheckout)
            .FirstOrDefaultAsync(cancellationToken);

    // The send is awaited before anything is written, so a failed email leaves the checkout
    // unreminded and the admin can retry. A failed send is logged and reported, not thrown.
    public async Task<ReminderSendResult> SendReminderAsync(int id, string siteBaseUrl, CancellationToken cancellationToken)
    {
        var checkout = await GetAbandonedCheckoutAsync(id, cancellationToken);
        if (checkout is null)
        {
            return ReminderSendResult.NotFound;
        }
        if (checkout.RemindedAt is not null)
        {
            return ReminderSendResult.AlreadyReminded;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RestoreTokenBytes));
        var reminder = new AbandonedCheckoutReminderView(
            checkout.Email,
            checkout.Lines,
            $"{siteBaseUrl.TrimEnd('/')}/cart/restore/{token}");

        var sent = await BestEffortEmail.TrySendAsync(
            () => emailSender.SendAbandonedCheckoutReminderAsync(reminder, cancellationToken),
            ex => logger.LogError(ex, "Failed to send abandoned-checkout reminder for pending checkout {PendingCheckoutId}", id),
            cancellationToken);
        if (!sent)
        {
            return ReminderSendResult.Failed;
        }

        var remindedAt = timeProvider.GetUtcNow();
        await db.PendingCheckouts
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.RemindedAt, remindedAt)
                .SetProperty(p => p.RestoreToken, token), cancellationToken);
        return ReminderSendResult.Sent;
    }
}
