using GlimpsesOfGlory.Abstractions.Payments;

namespace GlimpsesOfGlory.Abstractions.Orders;

public interface IOrderService
{
    // Returns null when the cart is empty or a line item no longer has enough
    // stock to satisfy the requested quantity.
    Task<PaymentIntentSetup?> CreatePaymentIntentAsync(ShippingAddressInfo address, CancellationToken cancellationToken);

    // Webhook-driven order finalization: idempotent, safe to call more than once
    // for the same PaymentIntent (e.g. duplicate webhook delivery).
    Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken);

    Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken);
}
