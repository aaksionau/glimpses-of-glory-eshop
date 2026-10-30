using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IOrderService
{
    // Returns null when the cart is empty or a line item no longer has enough stock to
    // satisfy the requested quantity. Pass the PaymentIntent id from a previous call
    // (e.g. from CheckoutSessionStore) to update that PaymentIntent/PendingCheckout in
    // place - on a page revisit (back button, refresh) - instead of creating a new one
    // each time; falls back to creating fresh if the existing one can no longer be used.
    // The returned cart is priced from the catalogue, as charged.
    Task<CheckoutPaymentSetup?> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken);

    // Webhook-driven order finalization: idempotent, safe to call more than once
    // for the same PaymentIntent (e.g. duplicate webhook delivery).
    Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken);

    // Places a pay-cash-at-pickup order straight away (no PaymentIntent/webhook round trip):
    // reserves stock, persists the Order as unpaid, and emails the confirmation. Returns the
    // new order's id, or null when the cart is empty or a line item no longer has enough stock.
    Task<int?> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken);

    Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken);

    Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken);
}
