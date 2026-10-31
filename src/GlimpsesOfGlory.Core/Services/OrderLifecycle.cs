using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Core.Services;

// Single source of truth for the order lifecycle. Each transition and rule is declared once here;
// the admin UI gets it via the AdminOrderActions built by Actions, AdminOrderService enforces it so
// a crafted POST can't skip it, and the admin lists and dashboard filter on the same rules in SQL.
public static class OrderLifecycle
{
    // What the admin can do to an order with these facts. The one place the detail page's actions
    // (and the list rows' refund flag) are read off the rules below, so the two can't drift.
    public static AdminOrderActions Actions(OrderFacts facts) => new()
    {
        CanMarkShipped = MarkShipped.IsAllowed(facts),
        CanMarkReadyForPickup = MarkReadyForPickup.IsAllowed(facts),
        CanMarkPickedUp = MarkPickedUp.IsAllowed(facts),
        CanMarkPaid = MarkPaid.IsAllowed(facts),
        CanMarkCancelled = MarkCancelled.IsAllowed(facts),
        NeedsRefund = NeedsRefund.IsSatisfiedBy(facts),
        CanResendConfirmation = CanResendConfirmation.IsSatisfiedBy(facts),
        CanResendShippedEmail = CanResendShippedEmail.IsSatisfiedBy(facts),
        CanResendReadyForPickupEmail = CanResendReadyForPickupEmail.IsSatisfiedBy(facts),
    };

    // Shipping orders go New -> Shipped, which is final.
    public static readonly OrderTransition MarkShipped = new(
        new(s => s.Status == OrderStatus.New && s.FulfillmentMethod == FulfillmentMethod.Shipping),
        OrderStatus.Shipped,
        o => o.ShippedAt);

    // Pickup orders go New -> ReadyForPickup -> PickedUp.
    public static readonly OrderTransition MarkReadyForPickup = new(
        new(s => s.Status == OrderStatus.New && s.FulfillmentMethod == FulfillmentMethod.Pickup),
        OrderStatus.ReadyForPickup,
        o => o.ReadyForPickupAt);

    public static readonly OrderTransition MarkPickedUp = new(
        new(s => s.Status == OrderStatus.ReadyForPickup),
        OrderStatus.PickedUp,
        o => o.PickedUpAt);

    public static readonly OrderTransition MarkCancelled = new(
        new(s => s.Status == OrderStatus.New),
        OrderStatus.Cancelled,
        o => o.CancelledAt);

    // Records the payment without moving the order along.
    public static readonly OrderTransition MarkPaid = new(
        new(s => !s.IsPaid && s.Status != OrderStatus.Cancelled),
        targetStatus: null,
        o => o.PaidAt);

    // Resending a customer email never changes the order. A cancelled order's confirmation would
    // mislead the buyer, so it's withheld.
    public static readonly OrderRule CanResendConfirmation = new(s => s.Status != OrderStatus.Cancelled);

    // Each fulfilment email only fits while the order is in that step; once a pickup order is
    // collected, "ready for pickup" is stale.
    public static readonly OrderRule CanResendShippedEmail = new(s => s.Status == OrderStatus.Shipped);

    public static readonly OrderRule CanResendReadyForPickupEmail = new(s => s.Status == OrderStatus.ReadyForPickup);

    // Cancelling doesn't touch the money, so a paid card order needs refunding by hand in Stripe.
    // Cash is settled in person and is out of scope.
    public static readonly OrderRule NeedsRefund = new(s =>
        s.Status == OrderStatus.Cancelled && s.PaymentMethod == PaymentMethod.Card && s.IsPaid);

    // Placed and still waiting to be shipped or made ready for pickup.
    public static readonly OrderRule ToFulfill = MarkShipped.Guard.Or(MarkReadyForPickup.Guard);

    // Cash still to be collected on an order that hasn't been cancelled.
    public static readonly OrderRule UnpaidCash = new OrderRule(s => s.PaymentMethod == PaymentMethod.Cash).And(MarkPaid.Guard);

    // Highlighted in the admin orders list: still to be fulfilled, or money still to be collected.
    // ToFulfill and UnpaidCash are both subsets of it, so their dashboard links land on the Needs
    // action tab.
    public static readonly OrderRule NeedsAction = ToFulfill.Or(MarkPaid.Guard);

    // The Shipped tab: shipped, ready for pickup, or collected.
    public static readonly OrderRule ShippedOrCollected = new(s =>
        s.Status == OrderStatus.Shipped || s.Status == OrderStatus.ReadyForPickup || s.Status == OrderStatus.PickedUp);

    public static readonly OrderRule Cancelled = new(s => s.Status == OrderStatus.Cancelled);
}
