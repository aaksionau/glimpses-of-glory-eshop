using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace GlimpsesOfGlory.IntegrationTests;

// Guards the DI composition in Program.cs and its Program.*.cs area files: every app-owned
// registration keeps its lifetime, and the composed container can resolve all of them.
[Collection(PostgresCollectionDefinition.Name)]
[SuppressMessage("Reliability", "CA1001", Justification = "The factory is disposed in IAsyncLifetime.DisposeAsync, which xunit calls after each test.")]
public sealed class ServiceRegistrationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string AppNamespace = "GlimpsesOfGlory";

    private TestDatabase database = null!;
    private EshopWebApplicationFactory factory = null!;

    public async Task InitializeAsync()
    {
        database = await postgres.CreateDatabaseAsync();
        factory = new EshopWebApplicationFactory(database);
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    [Fact]
    public void AppRegistrations_KeepTheirServiceTypesAndLifetimes()
    {
        _ = factory.Services; // builds the host, running Program.cs

        string[] expected =
        [
            "AppDbContext: Scoped",
            "CheckoutSessionStore: Scoped",
            "DbContextOptions<AppDbContext>: Scoped",
            "IAbandonedCheckoutService: Scoped",
            "IAdminDashboardService: Scoped",
            "IAdminOrderService: Scoped",
            "IAdminProductService: Scoped",
            "ICartRestoreService: Scoped",
            "ICartService: Scoped",
            "ICartStore: Scoped",
            "IConfigureOptions<ResendOptions>: Singleton",
            "IConfigureOptions<StripeOptions>: Singleton",
            "IDbContextOptionsConfiguration<AppDbContext>: Scoped",
            "IEmailSender: Transient",
            "IInventoryStore: Scoped",
            "IOptionsChangeTokenSource<ResendOptions>: Singleton",
            "IOptionsChangeTokenSource<StripeOptions>: Singleton",
            "IOrderService: Scoped",
            "IPaymentGateway: Scoped",
            "IProductCatalogService: Scoped",
            "IProductPhotoStorage: Scoped",
            "IShippingSettingsService: Scoped",
            "OrderLineInventory: Scoped",
            "ProductPhotoStorageOptions: Singleton",
            "RazorViewToStringRenderer: Scoped",
            "TimeProvider: Singleton", // the framework's TryAdd ...
            "TimeProvider: Singleton", // ... and the app's own AddSingleton(TimeProvider.System)
        ];

        var actual = factory.AppRegistrations
            .Where(descriptor => IsAppOwned(descriptor.ServiceType) || descriptor.ServiceType == typeof(TimeProvider))
            .Select(descriptor => $"{Describe(descriptor.ServiceType)}: {descriptor.Lifetime}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AppRegistrations_AllResolveFromTheComposedContainer()
    {
        using var scope = factory.Services.CreateScope();

        var serviceTypes = factory.AppRegistrations
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => IsAppOwned(type) && !type.IsGenericTypeDefinition)
            .Distinct()
            .ToList();

        Assert.NotEmpty(serviceTypes);
        Assert.All(serviceTypes, type => Assert.NotNull(scope.ServiceProvider.GetRequiredService(type)));
    }

    private static bool IsAppOwned(Type type) =>
        type.Namespace?.StartsWith(AppNamespace, StringComparison.Ordinal) == true
        || (type.IsGenericType && type.GetGenericArguments().Any(IsAppOwned));

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;
}
