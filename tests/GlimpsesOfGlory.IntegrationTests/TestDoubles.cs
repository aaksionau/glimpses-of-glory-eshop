using System.Collections.Concurrent;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.IntegrationTests;

// IEmailSender double that records the shipped notifications it was asked to send.
internal sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<OrderShippedView> Shipped { get; } = new();

    public Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken)
    {
        Shipped.Enqueue(order);
        return Task.CompletedTask;
    }

    public Task SendReadyForPickupNotificationAsync(OrderReadyForPickupView order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendAbandonedCheckoutReminderAsync(AbandonedCheckoutReminderView reminder, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Passes every call to the real store, but fails restock number failOnRestock (1-based), after the
// earlier restocks have already been written inside the cancel transaction.
internal sealed class FailingRestockInventoryStore(IInventoryStore inner, int failOnRestock) : IInventoryStore
{
    private int restocks;

    public Task<bool> TryReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken) =>
        inner.TryReserveStockAsync(productId, quantity, cancellationToken);

    public Task ReservePreorderAsync(int productId, int quantity, CancellationToken cancellationToken) =>
        inner.ReservePreorderAsync(productId, quantity, cancellationToken);

    public Task RestockAsync(int productId, int quantity, CancellationToken cancellationToken) =>
        ++restocks == failOnRestock
            ? throw new InvalidOperationException("Simulated restock failure")
            : inner.RestockAsync(productId, quantity, cancellationToken);

    public Task ReleasePreorderAsync(int productId, int quantity, CancellationToken cancellationToken) =>
        inner.ReleasePreorderAsync(productId, quantity, cancellationToken);
}
