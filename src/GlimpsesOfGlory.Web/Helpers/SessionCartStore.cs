using System.Text.Json;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.Web.Helpers;

public sealed class SessionCartStore(IHttpContextAccessor httpContextAccessor) : ICartStore
{
    private const string SessionKey = "Cart";

    public Task<Cart> GetCartAsync(CancellationToken cancellationToken)
    {
        var json = Session.GetString(SessionKey);
        var cart = json is null ? new Cart() : JsonSerializer.Deserialize<Cart>(json) ?? new Cart();
        return Task.FromResult(cart);
    }

    public Task SaveCartAsync(Cart cart, CancellationToken cancellationToken)
    {
        Session.SetString(SessionKey, JsonSerializer.Serialize(cart));
        return Task.CompletedTask;
    }

    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");
}
