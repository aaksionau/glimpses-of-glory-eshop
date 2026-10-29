using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminOrderService
{
    Task<IReadOnlyList<AdminOrderSummary>> GetAllOrdersAsync(CancellationToken cancellationToken);

    Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken);

    // Sends a shipping-notification email to the buyer on success.
    Task<bool> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken);

    // For cash orders, once the payment has been collected in person.
    Task<bool> MarkPaidAsync(int id, CancellationToken cancellationToken);

    Task<bool> MarkCancelledAsync(int id, CancellationToken cancellationToken);
}
