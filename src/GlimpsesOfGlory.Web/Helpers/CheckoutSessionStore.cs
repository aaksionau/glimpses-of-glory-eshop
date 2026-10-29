using System.Text.Json;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

public sealed class CheckoutSessionStore(IHttpContextAccessor httpContextAccessor)
{
    private const string AddressSessionKey = "CheckoutAddress";
    private const string PaymentIntentSessionKey = "CheckoutPaymentIntentId";
    private const string PlacedOrderSessionKey = "CheckoutPlacedOrderId";

    public CheckoutAddress? GetAddress()
    {
        var json = Session.GetString(AddressSessionKey);
        return json is null ? null : JsonSerializer.Deserialize<CheckoutAddress>(json);
    }

    public void SaveAddress(CheckoutAddress address) =>
        Session.SetString(AddressSessionKey, JsonSerializer.Serialize(address));

    // The PaymentIntent created for the current in-progress checkout, if any - lets a
    // page revisit (back button, refresh) reuse/update it instead of minting a new
    // Stripe PaymentIntent and orphaning the previous PendingCheckout row each time.
    public string? GetPaymentIntentId() => Session.GetString(PaymentIntentSessionKey);

    public void SavePaymentIntentId(string paymentIntentId) =>
        Session.SetString(PaymentIntentSessionKey, paymentIntentId);

    // The last cash order placed from this session. Cash orders have no PaymentIntent id
    // to look the confirmation up by, so the confirmation page only shows an order whose
    // id matches this - stops anyone viewing another shopper's order by guessing ids.
    // Deliberately kept by Clear() so the confirmation page survives a refresh.
    public int? GetPlacedOrderId() => Session.GetInt32(PlacedOrderSessionKey);

    public void SavePlacedOrderId(int orderId) =>
        Session.SetInt32(PlacedOrderSessionKey, orderId);

    public void Clear()
    {
        Session.Remove(AddressSessionKey);
        Session.Remove(PaymentIntentSessionKey);
    }

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");
}
