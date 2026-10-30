using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Helpers;

// Wording for the customer emails the admin order page can resend.
public static class OrderEmails
{
    // The email sent on OrderStatus.Shipped - pickup orders get "ready for pickup" instead.
    public static string ShippedEmailName(FulfillmentMethod fulfillmentMethod) =>
        fulfillmentMethod == FulfillmentMethod.Pickup ? "ready-for-pickup email" : "shipping email";
}
