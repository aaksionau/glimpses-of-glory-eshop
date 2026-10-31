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

// Decides which of the actions an order allows (the CanMark* flags on AdminOrderActions) is shown
// as the single primary "next step" button; the rest are secondary. Cancel is never primary - it's
// rendered separately as a quieter destructive action.
public static class OrderActions
{
    // Fulfilment moves the order forward, so it outranks collecting cash.
    public static OrderAction? Primary(AdminOrderActions actions) => Available(actions).Cast<OrderAction?>().FirstOrDefault();

    public static IReadOnlyList<OrderAction> Secondary(AdminOrderActions actions) => Available(actions).Skip(1).ToList();

    public static string Label(OrderAction action) => action switch
    {
        OrderAction.MarkShipped => "Mark shipped",
        OrderAction.MarkReadyForPickup => "Mark ready for pickup",
        OrderAction.MarkPickedUp => "Mark picked up",
        _ => "Mark as paid",
    };

    private static IEnumerable<OrderAction> Available(AdminOrderActions actions)
    {
        if (actions.CanMarkShipped)
        {
            yield return OrderAction.MarkShipped;
        }

        if (actions.CanMarkReadyForPickup)
        {
            yield return OrderAction.MarkReadyForPickup;
        }

        if (actions.CanMarkPickedUp)
        {
            yield return OrderAction.MarkPickedUp;
        }

        if (actions.CanMarkPaid)
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

// Which of the resends an order allows (the CanResend* flags on AdminOrderActions), in display order.
public static class OrderResends
{
    public static IReadOnlyList<OrderResend> Available(AdminOrderActions actions)
    {
        var resends = new List<OrderResend>();
        if (actions.CanResendConfirmation)
        {
            resends.Add(OrderResend.ResendConfirmation);
        }
        if (actions.CanResendShippedEmail)
        {
            resends.Add(OrderResend.ResendShippedEmail);
        }
        if (actions.CanResendReadyForPickupEmail)
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
