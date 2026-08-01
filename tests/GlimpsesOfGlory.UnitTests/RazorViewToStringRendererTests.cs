using System.Diagnostics;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace GlimpsesOfGlory.UnitTests;

public class RazorViewToStringRendererTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }

    // Builds just enough of the ASP.NET Core Razor view engine (no Kestrel, no HTTP
    // request, no database) to render a .cshtml view to a string, mirroring how
    // Program.cs wires RazorViewToStringRenderer via AddRazorPages().
    private static RazorViewToStringRenderer CreateRenderer()
    {
        var webAssembly = typeof(RazorViewToStringRenderer).Assembly;

        var services = new ServiceCollection();

        var environment = new TestWebHostEnvironment
        {
            ApplicationName = webAssembly.GetName().Name!,
            ContentRootPath = AppContext.BaseDirectory,
        };
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(environment);

        var partManager = new ApplicationPartManager();
        partManager.ApplicationParts.Add(new CompiledRazorAssemblyPart(webAssembly));
        services.AddSingleton(partManager);

        services.AddSingleton(new DiagnosticListener("GlimpsesOfGlory.UnitTests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddLogging();
        services.AddRazorPages();

        var provider = services.BuildServiceProvider();

        return new RazorViewToStringRenderer(
            provider.GetRequiredService<IRazorViewEngine>(),
            provider.GetRequiredService<ITempDataProvider>(),
            provider);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderConfirmationTemplate_WithOrderDetails()
    {
        var renderer = CreateRenderer();
        var order = new OrderConfirmationView(
            OrderId: 42,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2)],
            Subtotal: 25.00m,
            ShippingCost: 5.00m,
            Total: 30.00m,
            CreatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var html = await renderer.RenderAsync("~/Emails/OrderConfirmation.cshtml", order);

        Assert.Contains("Order #42", html);
        Assert.Contains("Jane Shopper", html);
        Assert.Contains("Ceramic Mug", html);
        Assert.Contains("123 Main St", html);
        Assert.Contains("$30.00", html);
    }
}
