using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Infrastructure.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.Extensions.Options;

// Composition root, part of Program (see Program.cs): checkout, shipping, Stripe payments,
// inventory, order placement and abandoned-checkout reminders.
public partial class Program
{
    private static void AddCheckout(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<CheckoutSessionStore>();

        builder.Services.AddScoped<IShippingSettingsService, ShippingSettingsService>();

        builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
        builder.Services.AddScoped<IPaymentGateway>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
            return new StripePaymentGateway(options.SecretKey, options.WebhookSecret);
        });
        builder.Services.AddScoped<IInventoryStore, EfInventoryStore>();
        builder.Services.AddScoped<OrderLineInventory>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IAbandonedCheckoutService, AbandonedCheckoutService>();
    }
}
