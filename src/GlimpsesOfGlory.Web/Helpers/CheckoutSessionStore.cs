using System.Text.Json;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

public sealed class CheckoutSessionStore(IHttpContextAccessor httpContextAccessor)
{
    private const string SessionKey = "CheckoutAddress";

    public CheckoutAddress? GetAddress()
    {
        var json = Session.GetString(SessionKey);
        return json is null ? null : JsonSerializer.Deserialize<CheckoutAddress>(json);
    }

    public void SaveAddress(CheckoutAddress address) =>
        Session.SetString(SessionKey, JsonSerializer.Serialize(address));

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");
}
