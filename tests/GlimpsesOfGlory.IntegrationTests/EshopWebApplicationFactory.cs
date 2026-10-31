using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.UnitTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GlimpsesOfGlory.IntegrationTests;

// Boots the real Web pipeline (Program.cs) against one test's Postgres database, with the two
// services that would reach the outside world - Stripe and Resend - replaced by in-memory fakes.
public sealed class EshopWebApplicationFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    private readonly string photosPath = Directory.CreateTempSubdirectory("eshop-photos-").FullName;

    internal WebhookPaymentGateway PaymentGateway { get; } = new();

    internal RecordingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
        builder.UseSetting("ProductPhotos:StoragePath", photosPath);
        // An empty DSN disables the Sentry SDK, so test failures never reach GlitchTip.
        builder.UseSetting("Sentry:Dsn", string.Empty);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(PaymentGateway);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Directory.Delete(photosPath, recursive: true);
        }
    }
}
