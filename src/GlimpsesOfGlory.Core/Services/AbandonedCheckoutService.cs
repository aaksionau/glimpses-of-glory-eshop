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

public sealed partial class AbandonedCheckoutService(AppDbContext db, IEmailSender emailSender, TimeProvider timeProvider, ILogger<AbandonedCheckoutService> logger) : IAbandonedCheckoutService
{
    private const int RestoreTokenBytes = 32;

    private static readonly Expression<Func<PendingCheckout, AbandonedCheckout>> ToAbandonedCheckout = p => new AbandonedCheckout(
        p.Id,
        p.Email,
        p.Lines.OrderBy(l => l.Id).Select(l => new AbandonedCheckoutLine(l.ProductName, l.Quantity)).ToList(),
        p.Total,
        p.FulfillmentMethod,
        p.CreatedAt,
        p.RemindedAt);

    public async Task<IReadOnlyList<AbandonedCheckout>> GetAbandonedCheckoutsAsync(CancellationToken cancellationToken) =>
        await AdminQueryFilters.AbandonedCheckouts(db, timeProvider.GetUtcNow())
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToAbandonedCheckout)
            .ToListAsync(cancellationToken);

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
            ex => LogReminderEmailFailed(logger, ex, id),
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send abandoned-checkout reminder for pending checkout {PendingCheckoutId}")]
    private static partial void LogReminderEmailFailed(ILogger logger, Exception exception, int pendingCheckoutId);
}
