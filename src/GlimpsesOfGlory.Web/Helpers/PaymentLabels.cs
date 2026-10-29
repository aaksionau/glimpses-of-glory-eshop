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
}
