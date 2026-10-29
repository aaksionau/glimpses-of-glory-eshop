using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Core.Services;

// Single source of truth for which admin actions an order allows - the admin UI gets it via the
// CanMark* flags on AdminOrderDetail, and AdminOrderService enforces it so a crafted POST can't skip it.
public static class OrderTransitionPolicy
{
    // Shipped/ready-for-pickup and cancelled are both final for fulfillment.
    public static bool CanMarkShipped(OrderStatus status) => status == OrderStatus.New;

    public static bool CanMarkCancelled(OrderStatus status) => status == OrderStatus.New;

    public static bool CanMarkPaid(OrderStatus status, bool isPaid) => !isPaid && status != OrderStatus.Cancelled;

    // Cancelling doesn't touch the money, so a paid card order needs refunding by hand in Stripe.
    // Cash is settled in person and is out of scope.
    public static bool NeedsRefund(OrderStatus status, PaymentMethod paymentMethod, bool isPaid) =>
        status == OrderStatus.Cancelled && paymentMethod == PaymentMethod.Card && isPaid;
}
