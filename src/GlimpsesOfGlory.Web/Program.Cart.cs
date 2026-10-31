using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Web.Helpers;

// Composition root, part of Program (see Program.cs): the session-backed cart and cart restore links.
public partial class Program
{
    private static void AddCart(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICartStore, SessionCartStore>();
        builder.Services.AddScoped<ICartService, CartService>();
        builder.Services.AddScoped<ICartRestoreService, CartRestoreService>();
    }
}
