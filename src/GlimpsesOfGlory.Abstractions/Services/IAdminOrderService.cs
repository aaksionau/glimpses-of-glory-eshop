using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminOrderService
{
    // Newest first, filtered to the tab in the database.
    Task<IReadOnlyList<AdminOrderSummary>> GetOrdersAsync(AdminOrderTab tab, CancellationToken cancellationToken);

    Task<AdminOrderTabCounts> GetOrderTabCountsAsync(CancellationToken cancellationToken);

    Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken);

    // Sends a shipping-notification email to the buyer on success.
    Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, CancellationToken cancellationToken);

    // Pickup orders only, once the buyer has collected an order that was ready for pickup.
    Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken);

    // For cash orders, once the payment has been collected in person.
    Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken);

    Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken);
}
