using GlimpsesOfGlory.Abstractions.Payments;

namespace GlimpsesOfGlory.Abstractions.Orders;

public interface IOrderService
{
    // Returns null when the cart is empty or a line item no longer has enough stock to
    // satisfy the requested quantity. Pass the PaymentIntent id from a previous call
    // (e.g. from CheckoutSessionStore) to update that PaymentIntent/PendingCheckout in
    // place - on a page revisit (back button, refresh) - instead of creating a new one
    // each time; falls back to creating fresh if the existing one can no longer be used.
    Task<PaymentIntentSetup?> CreatePaymentIntentAsync(ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken);

    // Webhook-driven order finalization: idempotent, safe to call more than once
    // for the same PaymentIntent (e.g. duplicate webhook delivery).
    Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken);

    Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken);
}
