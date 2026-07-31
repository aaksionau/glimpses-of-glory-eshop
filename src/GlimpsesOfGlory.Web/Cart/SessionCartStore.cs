using System.Text.Json;
using GlimpsesOfGlory.Abstractions.Cart;
using CartModel = GlimpsesOfGlory.Abstractions.Cart.Cart;

namespace GlimpsesOfGlory.Web.Cart;

public sealed class SessionCartStore(IHttpContextAccessor httpContextAccessor) : ICartStore
{
    private const string SessionKey = "Cart";

    public Task<CartModel> GetCartAsync(CancellationToken cancellationToken)
    {
        var json = Session.GetString(SessionKey);
        var cart = json is null ? new CartModel() : JsonSerializer.Deserialize<CartModel>(json) ?? new CartModel();
        return Task.FromResult(cart);
    }

    public Task SaveCartAsync(CartModel cart, CancellationToken cancellationToken)
    {
        Session.SetString(SessionKey, JsonSerializer.Serialize(cart));
        return Task.CompletedTask;
    }

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");
}
