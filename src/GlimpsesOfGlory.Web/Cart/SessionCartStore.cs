using System.Text.Json;
using GlimpsesOfGlory.Application.Cart;
using DomainCart = GlimpsesOfGlory.Domain.Cart;

namespace GlimpsesOfGlory.Web.Cart;

public sealed class SessionCartStore(IHttpContextAccessor httpContextAccessor) : ICartStore
{
    private const string SessionKey = "Cart";

    public Task<DomainCart> GetCartAsync(CancellationToken cancellationToken)
    {
        var json = Session.GetString(SessionKey);
        var cart = json is null ? new DomainCart() : JsonSerializer.Deserialize<DomainCart>(json) ?? new DomainCart();
        return Task.FromResult(cart);
    }

    public Task SaveCartAsync(DomainCart cart, CancellationToken cancellationToken)
    {
        Session.SetString(SessionKey, JsonSerializer.Serialize(cart));
        return Task.CompletedTask;
    }

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");
}
