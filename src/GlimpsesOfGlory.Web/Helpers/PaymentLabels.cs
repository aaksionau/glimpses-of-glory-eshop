using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Helpers;

public static class PaymentLabels
{
    // Card orders are always paid by the time they exist; cash orders until the admin marks them paid.
    public static string Status(PaymentMethod paymentMethod, bool isPaid) => paymentMethod switch
    {
        PaymentMethod.Cash => isPaid ? "Cash (paid)" : "Cash at pickup (unpaid)",
        _ => "Card",
    };

    // Shown wherever an unpaid order is still to be collected.
    public static string CashDueNote(decimal total) =>
        $"Please bring {total:C} in cash when you collect your order.";
}
