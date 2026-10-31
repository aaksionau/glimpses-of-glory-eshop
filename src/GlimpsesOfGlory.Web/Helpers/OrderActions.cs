using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

// The forward (non-cancel) steps the admin order page can offer. Member names double as the
// DetailsModel page handler names (OnPost{Action}Async) - a unit test pins that.
public enum OrderAction
{
    MarkShipped,
    MarkReadyForPickup,
    MarkPickedUp,
    MarkPaid,
}

// Decides which of the actions an order allows (the CanMark* flags) is shown as the single
// primary "next step" button; the rest are secondary. Cancel is never primary - it's rendered
// separately as a quieter destructive action.
public static class OrderActions
{
    // Fulfilment moves the order forward, so it outranks collecting cash.
    public static OrderAction? Primary(AdminOrderDetail order) => Available(order).Cast<OrderAction?>().FirstOrDefault();

    public static IReadOnlyList<OrderAction> Secondary(AdminOrderDetail order) => Available(order).Skip(1).ToList();

    public static string Label(OrderAction action) => action switch
    {
        OrderAction.MarkShipped => "Mark shipped",
        OrderAction.MarkReadyForPickup => "Mark ready for pickup",
        OrderAction.MarkPickedUp => "Mark picked up",
        _ => "Mark as paid",
    };

    private static IEnumerable<OrderAction> Available(AdminOrderDetail order)
    {
        if (order.CanMarkShipped)
        {
            yield return OrderAction.MarkShipped;
        }

        if (order.CanMarkReadyForPickup)
        {
            yield return OrderAction.MarkReadyForPickup;
        }

        if (order.CanMarkPickedUp)
        {
            yield return OrderAction.MarkPickedUp;
        }

        if (order.CanMarkPaid)
        {
            yield return OrderAction.MarkPaid;
        }
    }
}

// The customer emails the admin order page can resend. Member names double as the DetailsModel
// page handler names (OnPost{Resend}Async) - a unit test pins that.
public enum OrderResend
{
    ResendConfirmation,
    ResendShippedEmail,
    ResendReadyForPickupEmail,
}

// Which of the resends an order allows (the CanResend* flags), in display order.
public static class OrderResends
{
    public static IReadOnlyList<OrderResend> Available(AdminOrderDetail order)
    {
        var resends = new List<OrderResend>();
        if (order.CanResendConfirmation)
        {
            resends.Add(OrderResend.ResendConfirmation);
        }
        if (order.CanResendShippedEmail)
        {
            resends.Add(OrderResend.ResendShippedEmail);
        }
        if (order.CanResendReadyForPickupEmail)
        {
            resends.Add(OrderResend.ResendReadyForPickupEmail);
        }
        return resends;
    }

    public static string Label(OrderResend resend) => resend switch
    {
        OrderResend.ResendConfirmation => "Resend confirmation",
        OrderResend.ResendShippedEmail => "Resend shipping email",
        _ => "Resend ready-for-pickup email",
    };
}
