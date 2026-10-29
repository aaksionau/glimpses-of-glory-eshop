using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminOrderService
{
    Task<IReadOnlyList<AdminOrderSummary>> GetAllOrdersAsync(CancellationToken cancellationToken);

    Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken);

    // Sends a shipping-notification email to the buyer on success.
    Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken);

    // For cash orders, once the payment has been collected in person.
    Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken);

    Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken);
}
