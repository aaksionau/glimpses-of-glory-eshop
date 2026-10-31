using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Xml.Linq;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;
using GlimpsesOfGlory.Core.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// The minimal endpoints (robots.txt, sitemap.xml, the Stripe webhook) and the admin auth
// convention, exercised over HTTP through the real pipeline in Program.cs.
[Collection(PostgresCollectionDefinition.Name)]
[SuppressMessage("Reliability", "CA1001", Justification = "The factory is disposed in IAsyncLifetime.DisposeAsync, which xunit calls after each test.")]
public sealed class EndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string PaymentIntentId = "pi_endpoint_test";

    private TestDatabase database = null!;
    private EshopWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        database = await postgres.CreateDatabaseAsync();
        factory = new EshopWebApplicationFactory(database);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    [Fact]
    public async Task StripeWebhook_WithABadSignature_Returns400_AndChangesNoOrder()
    {
        await SeedPendingCheckoutAsync();
        // Would confirm the order if the signature were accepted.
        factory.PaymentGateway.NextWebhookResult = new(PaymentEventOutcome.Succeeded, PaymentIntentId, null);

        using var response = await PostWebhookAsync(signature: "t=1,v1=forged");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCheckoutStillPendingAsync();
    }

    [Fact]
    public async Task StripeWebhook_WithASucceededPayment_ConfirmsThePendingCheckoutAsAnOrder()
    {
        var productId = await SeedPendingCheckoutAsync();
        factory.PaymentGateway.NextWebhookResult = new(PaymentEventOutcome.Succeeded, PaymentIntentId, null);

        using var response = await PostWebhookAsync(FakePaymentGateway.ValidSignature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = database.CreateDb();
        Assert.Empty(db.PendingCheckouts);
        var order = await db.Orders.Include(o => o.Lines).SingleAsync();
        Assert.Equal(PaymentIntentId, order.StripePaymentIntentId);
        Assert.Equal("shopper@example.com", order.Email);
        Assert.Equal(PaymentMethod.Card, order.PaymentMethod);
        var line = Assert.Single(order.Lines);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(3, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Single(factory.EmailSender.Confirmations);
    }

    [Theory]
    [InlineData(PaymentEventOutcome.Failed)]
    [InlineData(PaymentEventOutcome.Irrelevant)]
    public async Task StripeWebhook_WithANonSuccessEvent_Returns200_AndChangesNoOrder(PaymentEventOutcome outcome)
    {
        await SeedPendingCheckoutAsync();
        factory.PaymentGateway.NextWebhookResult = new(outcome, PaymentIntentId, outcome == PaymentEventOutcome.Failed ? "Card declined" : null);

        using var response = await PostWebhookAsync(FakePaymentGateway.ValidSignature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCheckoutStillPendingAsync();
    }

    [Fact]
    public async Task Sitemap_ListsTheActiveProductSlugs()
    {
        await using (var db = database.CreateDb())
        {
            db.Products.AddRange(
                NewProduct("harbour-at-dawn"),
                NewProduct("sunrise-over-hills"),
                NewProduct("retired-print", isActive: false));
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locations = XDocument.Parse(await response.Content.ReadAsStringAsync())
            .Descendants(ns + "loc")
            .Select(loc => loc.Value)
            .ToList();
        Assert.Equal(
            [
                "https://localhost/",
                "https://localhost/products",
                "https://localhost/products/harbour-at-dawn",
                "https://localhost/products/sunrise-over-hills",
            ],
            locations);
    }

    [Fact]
    public async Task RobotsTxt_DisallowsAdminCartAndCheckout_AndPointsAtTheSitemap()
    {
        using var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var lines = (await response.Content.ReadAsStringAsync()).Split('\n').Select(line => line.Trim()).ToList();
        Assert.Contains("Disallow: /admin", lines);
        Assert.Contains("Disallow: /cart", lines);
        Assert.Contains("Disallow: /checkout", lines);
        Assert.Contains("Sitemap: https://localhost/sitemap.xml", lines);
    }

    [Fact]
    public async Task AdminPage_RequestedAnonymously_RedirectsToTheAdminLogin()
    {
        using var response = await client.GetAsync(new Uri("/Admin/Orders", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("/Admin/Login", location.AbsolutePath);
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/webhooks/stripe", UriKind.Relative))
        {
            Content = new StringContent("""{"type":"payment_intent.succeeded"}"""),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return await client.SendAsync(request);
    }

    private async Task<int> SeedPendingCheckoutAsync()
    {
        await using var db = database.CreateDb();
        var product = NewProduct("harbour-at-dawn");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.PendingCheckouts.Add(new PendingCheckout
        {
            StripePaymentIntentId = PaymentIntentId,
            Email = "shopper@example.com",
            FulfillmentMethod = FulfillmentMethod.Pickup,
            ShippingAddress = ShippingAddress.FromInfo(new ShippingAddressInfo("shopper@example.com", "Sam Shopper", null, null, null, null, null, null)),
            Subtotal = 30m,
            ShippingCost = 0m,
            Lines = [new PendingCheckoutLine { ProductId = product.Id, ProductName = product.Name, UnitPrice = 15m, Quantity = 2 }],
        });
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task AssertCheckoutStillPendingAsync()
    {
        await using var db = database.CreateDb();
        Assert.Empty(db.Orders);
        var pending = await db.PendingCheckouts.SingleAsync();
        Assert.Equal(PaymentIntentId, pending.StripePaymentIntentId);
        Assert.Equal(5, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Empty(factory.EmailSender.Confirmations);
    }

    private static Product NewProduct(string slug, bool isActive = true) =>
        new() { Name = slug, Slug = slug, Description = "A print", Price = 15m, StockQuantity = 5, IsActive = isActive };
}
