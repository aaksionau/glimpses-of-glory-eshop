using System.Net;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Email;

namespace GlimpsesOfGlory.UnitTests;

public class RazorViewToStringRendererTests
{
    [Fact]
    public async Task RenderAsync_RendersOrderConfirmationTemplate_WithOrderDetails()
    {
        var renderer = TestRazorRenderer.Create();
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
    public async Task RenderAsync_RendersOrderShippedTemplate_WithTrackingLink()
    {
        const string url = "https://tools.usps.com/go/TrackConfirmAction?tLabels=9400111899223";
        var order = new OrderShippedView(
            OrderId: 44,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", "123 Main St", null, "Springfield", "IL", "62704", "US"),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 1, false, null)],
            Total: 17.50m,
            TrackingNumber: "9400111899223",
            TrackingUrl: url);

        var html = WebUtility.HtmlDecode(await TestRazorRenderer.Create().RenderAsync("~/Emails/OrderShipped.cshtml", order));

        Assert.Contains($"<a href=\"{url}\"><strong>9400111899223</strong></a>", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderReadyForPickupTemplate_ForUnpaidCashOrder_AsksForCash()
    {
        var order = new OrderReadyForPickupView(
            OrderId: 45,
            IsPaid: false,
            Address: new ShippingAddressInfo("shopper@example.com", "Jane Shopper", null, null, null, null, null, null),
            Lines: [new OrderConfirmationLine("Ceramic Mug", 12.50m, 2, false, null)],
            Total: 25.00m);

        var html = await TestRazorRenderer.Create().RenderAsync("~/Emails/OrderReadyForPickup.cshtml", order);

        Assert.Contains("Order #45 is ready to collect", html);
        Assert.Contains("bring $25.00 in cash", html);
        Assert.DoesNotContain("Shipping to", html);
    }

    [Fact]
    public async Task RenderAsync_RendersOrderConfirmationTemplate_ForCashOrder_AsksForCash()
    {
        var renderer = TestRazorRenderer.Create();
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
}
