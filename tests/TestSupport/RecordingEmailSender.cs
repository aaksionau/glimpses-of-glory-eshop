using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.TestSupport;

// IEmailSender double that records each email instead of sending it, or fails on demand. Not
// thread-safe, but in the Postgres race tests only the winning transition sends an email.
internal sealed class RecordingEmailSender : IEmailSender
{
    public List<OrderShippedView> Shipped { get; } = [];

    public List<OrderReadyForPickupView> ReadyForPickup { get; } = [];

    public List<OrderConfirmationView> Confirmations { get; } = [];

    public List<AbandonedCheckoutReminderView> Reminders { get; } = [];

    // Simulates the email provider rejecting the send.
    public bool Fail { get; init; }

    // Simulates an HttpClient timeout, which surfaces as a TaskCanceledException.
    public bool TimeOut { get; init; }

    public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Record(Confirmations, order);

    public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken) => Record(Shipped, order);

    public Task SendReadyForPickupNotificationAsync(OrderReadyForPickupView order, CancellationToken cancellationToken) => Record(ReadyForPickup, order);

    public Task SendAbandonedCheckoutReminderAsync(AbandonedCheckoutReminderView reminder, CancellationToken cancellationToken) => Record(Reminders, reminder);

    private Task Record<T>(List<T> sent, T email)
    {
        if (Fail)
        {
            throw new HttpRequestException("Resend rejected email (500)");
        }
        if (TimeOut)
        {
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        }

        sent.Add(email);
        return Task.CompletedTask;
    }
}
