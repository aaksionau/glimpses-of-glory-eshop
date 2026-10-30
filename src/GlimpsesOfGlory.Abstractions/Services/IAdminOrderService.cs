using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IAdminOrderService
{
    // Newest first, filtered to the tab and filter and paged in the database. A page past the end
    // returns the last page instead.
    Task<AdminOrderPage> GetOrdersAsync(AdminOrderTab tab, AdminOrderFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    // How many orders each tab holds under the same filter, so the tab counts match the list.
    Task<AdminOrderTabCounts> GetOrderTabCountsAsync(AdminOrderFilter filter, CancellationToken cancellationToken);

    // Every order placed from the start of `from` to the end of `to` (UTC days, inclusive), oldest
    // first, streamed from the database so a large range is never held in memory. Pass the
    // cancellation token with WithCancellation when enumerating.
    IAsyncEnumerable<AdminOrderExportRow> StreamOrdersForExport(DateOnly from, DateOnly to);

    Task<AdminOrderDetail?> GetOrderAsync(int id, CancellationToken cancellationToken);

    // Sends a shipping-notification email to the buyer on success.
    Task<OrderUpdateResult> MarkShippedAsync(int id, string? trackingNumber, ShippingCarrier carrier, CancellationToken cancellationToken);

    // Pickup orders only, once the buyer has collected an order that was ready for pickup.
    Task<OrderUpdateResult> MarkPickedUpAsync(int id, CancellationToken cancellationToken);

    // For cash orders, once the payment has been collected in person.
    Task<OrderUpdateResult> MarkPaidAsync(int id, CancellationToken cancellationToken);

    // Replaces the order's admin-only note; a blank note clears it. Allowed in any status.
    Task<OrderUpdateResult> UpdateInternalNoteAsync(int id, string? note, CancellationToken cancellationToken);

    Task<OrderUpdateResult> MarkCancelledAsync(int id, CancellationToken cancellationToken);

    // Re-sends the buyer's order confirmation email; any order that isn't cancelled. Changes nothing on the order.
    Task<EmailResendResult> ResendConfirmationAsync(int id, CancellationToken cancellationToken);

    // Re-sends the shipped (or ready-for-pickup) email; only while the order is shipped/ready. Changes nothing on the order.
    Task<EmailResendResult> ResendShippedEmailAsync(int id, CancellationToken cancellationToken);
}
