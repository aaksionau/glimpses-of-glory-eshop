using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutConfirmationTests
{
    private const string ResultPath = "~/Pages/Checkout/_ConfirmationResult.cshtml";
    private const string PollPath = "~/Pages/Checkout/_PaymentPoll.cshtml";

    [Theory]
    [InlineData(FulfillmentMethod.Shipping, "We'll email you a tracking number as soon as your order ships.")]
    [InlineData(FulfillmentMethod.Pickup, "We'll email you when your order is ready to collect.")]
    public async Task PlacedOrder_StatesTheConfirmationEmail_WhatHappensNext_AndOffersContinueShopping(FulfillmentMethod method, string nextSteps)
    {
        var page = await LoadAsync(Order(method), attempt: 0, htmx: false);

        var html = await RenderAsync(ResultPath, page);
        var text = CheckoutOrderSummaryTests.Text(html);

        Assert.Contains("A confirmation has been sent to shopper@example.com", text);
        Assert.Contains(nextSteps, text);
        Assert.Matches("<a href=\"/products\"[^>]*>Continue shopping</a>", html);
    }

    [Fact]
    public void Page_HasNoMetaRefresh_AndWrapsTheResultInAPoliteLiveRegion()
    {
        var page = File.ReadAllText(Path.Combine(CheckoutPaymentFormTests.WebProjectDirectory(), "Pages", "Checkout", "Confirmation.cshtml"));

        Assert.DoesNotContain("http-equiv", page);
        Assert.Matches("<div id=\"confirmation-status\" aria-live=\"polite\">\\s*<partial name=\"_ConfirmationResult\" model=\"Model\" />\\s*</div>", page);
        Assert.Contains("<partial name=\"_PaymentPoll\" model=\"Model\" />", page);
    }

    [Fact]
    public async Task WhilePaymentIsPending_ThePageShowsAPollerForTheNextAttempt()
    {
        var page = await LoadAsync(order: null, attempt: 0, htmx: false);

        var html = await RenderAsync(PollPath, page);

        var poller = Regex.Match(html, "<div id=\"payment-poll\"[^>]*>").Value;
        Assert.Contains("hx-get=\"/checkout/confirmation?payment_intent=pi_123&amp;attempt=1\"", poller);
        Assert.Contains("hx-trigger=\"every 2s\"", poller);
        Assert.Contains("hx-swap=\"outerHTML\"", poller);
        Assert.Contains("Confirming your payment", CheckoutOrderSummaryTests.Text(await RenderAsync(ResultPath, page)));
    }

    [Fact]
    public async Task HtmxPoll_StillPending_ReturnsOnlyTheNextPoller_LeavingTheLiveRegionAlone()
    {
        var page = await LoadAsync(order: null, attempt: 2, htmx: true);
        var result = await page.OnGetAsync("pi_123", null, 2, CancellationToken.None);

        Assert.Equal("_PaymentPoll", Assert.IsType<PartialViewResult>(result).ViewName);
        var html = await RenderAsync(PollPath, page);
        Assert.Contains("attempt=3", html);
        Assert.DoesNotContain("confirmation-status", html);
    }

    [Fact]
    public async Task HtmxPoll_OrderFound_StopsPolling_FillsTheLiveRegion_AndClearsTheCartCount()
    {
        var page = await LoadAsync(Order(FulfillmentMethod.Shipping), attempt: 1, htmx: true);

        var html = await RenderAsync(PollPath, page);

        Assert.DoesNotContain("hx-trigger", html);
        Assert.Matches("<div id=\"confirmation-status\" hx-swap-oob=\"innerHTML\">", html);
        Assert.Contains("Thank you for your order!", CheckoutOrderSummaryTests.Text(html));
        Assert.Matches("<span id=\"cart-count\" hx-swap-oob=\"innerHTML\"></span>", html);
    }

    [Fact]
    public async Task HtmxPoll_OutOfAttempts_StopsPolling_AndAnnouncesTheFallback()
    {
        var page = await LoadAsync(order: null, attempt: ConfirmationModel.MaxPollAttempts, htmx: true);

        var html = await RenderAsync(PollPath, page);

        Assert.DoesNotContain("hx-trigger", html);
        Assert.Matches("<div id=\"confirmation-status\" hx-swap-oob=\"innerHTML\">", html);
        Assert.Contains("Payment received", CheckoutOrderSummaryTests.Text(html));
        Assert.DoesNotContain("cart-count", html);
    }

    [Theory]
    [InlineData(-999, 0)]
    [InlineData(999, ConfirmationModel.MaxPollAttempts)]
    public async Task ForgedAttemptCount_IsClampedServerSide(int forged, int expected)
    {
        var page = await LoadAsync(order: null, attempt: forged, htmx: true);

        Assert.Equal(expected, page.Attempt);
    }

    [Fact]
    public async Task Response_VariesOnTheHtmxHeader_SoCachesDontMixPageAndFragment()
    {
        var page = await LoadAsync(order: null, attempt: 0, htmx: true);

        Assert.Equal("HX-Request", page.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task FullPageLoad_WithAResult_RendersNoPoller()
    {
        var page = await LoadAsync(Order(FulfillmentMethod.Shipping), attempt: 0, htmx: false);

        Assert.Equal(string.Empty, (await RenderAsync(PollPath, page)).Trim());
    }

    private static OrderConfirmationView Order(FulfillmentMethod method) => new(
        OrderId: 42,
        FulfillmentMethod: method,
        IsPaid: true,
        Address: method == FulfillmentMethod.Pickup
            ? new ShippingAddressInfo("shopper@example.com", "Sam Shopper", null, null, null, null, null, null)
            : new ShippingAddressInfo("shopper@example.com", "Sam Shopper", "1 Main St", null, "Springfield", "IL", "62701", "US"),
        Lines: [new OrderConfirmationLine("Sunrise print", 25m, 2, false, null)],
        Subtotal: 50m,
        ShippingCost: 5m,
        Total: 55m,
        CreatedAt: DateTimeOffset.UnixEpoch);

    private static async Task<ConfirmationModel> LoadAsync(OrderConfirmationView? order, int attempt, bool htmx)
    {
        var httpContext = new DefaultHttpContext
        {
            Session = new InMemorySession(),
            RequestServices = new ServiceCollection()
                .AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>()
                .BuildServiceProvider(),
        };
        if (htmx)
        {
            httpContext.Request.Headers["HX-Request"] = "true";
        }

        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        var page = new ConfirmationModel(new StubOrderService(order), new StubCartService(), sessionStore)
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            },
        };
        await page.OnGetAsync("pi_123", null, attempt, CancellationToken.None);
        return page;
    }

    private static Task<string> RenderAsync(string path, ConfirmationModel model) =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(path, model);

    private sealed class StubCartService : ICartService
    {
        public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLineAsync(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubOrderService(OrderConfirmationView? order) : IOrderService
    {
        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => Task.FromResult(order);

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
