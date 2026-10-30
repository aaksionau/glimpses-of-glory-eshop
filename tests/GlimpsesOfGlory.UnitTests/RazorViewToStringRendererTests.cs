using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
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
    internal static RazorViewToStringRenderer CreateRenderer()
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
            FulfillmentMethod: FulfillmentMethod.Shipping,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
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

    [Fact]
    public async Task RenderAsync_RendersOrderConfirmationTemplate_ForPickupOrder()
    {
        var renderer = CreateRenderer();
        var order = new OrderConfirmationView(
            OrderId: 44,
            FulfillmentMethod: FulfillmentMethod.Pickup,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", null, null, null, null, null, null),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Subtotal: 25.00m,
            ShippingCost: 0m,
            Total: 25.00m,
            CreatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var html = await renderer.RenderAsync("~/Emails/OrderConfirmation.cshtml", order);

        Assert.Contains("Local pickup", html);
        Assert.DoesNotContain("Shipping to", html);
        Assert.Contains("$25.00", html);
    }

    // OrderNotifier.SendShippedNotificationAsync renders this same template before sending
    // via Resend, so exercising it directly (as above for OrderConfirmation) covers the
    // order/tracking details without needing a real mailbox.
    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_WithTrackingNumber()
    {
        var renderer = CreateRenderer();
        var order = new OrderShippedView(
            OrderId: 42,
            FulfillmentMethod: FulfillmentMethod.Shipping,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Total: 30.00m,
            TrackingNumber: "1Z999AA10123456784",
            TrackingUrl: null);

        var html = await renderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);

        Assert.Contains("Order #42", html);
        Assert.Contains("Jane Shopper", html);
        Assert.Contains("Ceramic Mug", html);
        Assert.Contains("1Z999AA10123456784", html);
        Assert.Contains("$30.00", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_WithTrackingLink()
    {
        const string url = "https://tools.usps.com/go/TrackConfirmAction?tLabels=9400111899223";
        var order = new OrderShippedView(
            OrderId: 44,
            FulfillmentMethod: FulfillmentMethod.Shipping,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 1, false, null)],
            Total: 17.50m,
            TrackingNumber: "9400111899223",
            TrackingUrl: url);

        var html = WebUtility.HtmlDecode(await CreateRenderer().RenderAsync("~/Emails/OrderShipped.cshtml", order));

        Assert.Contains($"<a href=\"{url}\"><strong>9400111899223</strong></a>", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_WithoutTrackingLink_ShowsPlainTrackingNumber()
    {
        var order = new OrderShippedView(
            OrderId: 45,
            FulfillmentMethod: FulfillmentMethod.Shipping,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 1, false, null)],
            Total: 17.50m,
            TrackingNumber: "ABC123",
            TrackingUrl: null);

        var html = await CreateRenderer().RenderAsync("~/Emails/OrderShipped.cshtml", order);

        Assert.Contains("<strong>ABC123</strong>", html);
        Assert.DoesNotContain("<a href", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_WithoutTrackingNumber()
    {
        var renderer = CreateRenderer();
        var order = new OrderShippedView(
            OrderId: 43,
            FulfillmentMethod: FulfillmentMethod.Shipping,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 1, false, null)],
            Total: 17.50m,
            TrackingNumber: null,
            TrackingUrl: null);

        var html = await renderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);

        Assert.Contains("Order #43", html);
        Assert.DoesNotContain("Tracking number", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_ForPickupOrder()
    {
        var renderer = CreateRenderer();
        var order = new OrderShippedView(
            OrderId: 44,
            FulfillmentMethod: FulfillmentMethod.Pickup,
            IsPaid: true,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", null, null, null, null, null, null),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Total: 25.00m,
            TrackingNumber: null,
            TrackingUrl: null);

        var html = await renderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);

        Assert.Contains("ready for pickup", html);
        Assert.DoesNotContain("on its way", html);
        Assert.DoesNotContain("Shipping to", html);
        Assert.DoesNotContain("in cash", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderShippedTemplate_ForUnpaidCashOrder_AsksForCash()
    {
        var renderer = CreateRenderer();
        var order = new OrderShippedView(
            OrderId: 45,
            FulfillmentMethod: FulfillmentMethod.Pickup,
            IsPaid: false,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", null, null, null, null, null, null),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Total: 25.00m,
            TrackingNumber: null,
            TrackingUrl: null);

        var html = await renderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);

        Assert.Contains("bring $25.00 in cash", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderConfirmationTemplate_ForCashOrder_AsksForCash()
    {
        var renderer = CreateRenderer();
        var order = new OrderConfirmationView(
            OrderId: 46,
            FulfillmentMethod: FulfillmentMethod.Pickup,
            IsPaid: false,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", null, null, null, null, null, null),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Subtotal: 25.00m,
            ShippingCost: 0m,
            Total: 25.00m,
            CreatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var html = await renderer.RenderAsync("~/Emails/OrderConfirmation.cshtml", order);

        Assert.Contains("bring $25.00 in cash", html);
    }

    [Fact]
    public async Task RenderAsync_RendersAdminSidebar_HighlightingOnlyTheCurrentSection()
    {
        var renderer = CreateRenderer();

        var html = await renderer.RenderAsync("~/Pages/Admin/_AdminSidebarNav.cshtml", AdminSection.Orders);

        Assert.Contains("Dashboard", html);
        Assert.Contains("Products", html);
        Assert.Contains("Shipping rates", html);
        Assert.Contains("Sign out", html);
        Assert.Single(Regex.Matches(html, "aria-current=\"page\""));
        Assert.Matches("aria-current=\"page\"[^>]*>Orders</a>", html);
        Assert.DoesNotContain("Cart", html);
    }
}
