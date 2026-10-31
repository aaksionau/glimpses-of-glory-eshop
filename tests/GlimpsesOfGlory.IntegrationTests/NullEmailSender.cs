using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.IntegrationTests;

// IEmailSender double that sends nothing.
internal sealed class NullEmailSender : IEmailSender
{
    public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendReadyForPickupNotificationAsync(OrderReadyForPickupView order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendAbandonedCheckoutReminderAsync(AbandonedCheckoutReminderView reminder, CancellationToken cancellationToken) => Task.CompletedTask;
}
