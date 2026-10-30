using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutPickupPaymentTests
{
    private const string PartialPath = "~/Pages/Checkout/_PickupPaymentChoice.cshtml";

    [Fact]
    public async Task Choice_IsARadioGroupOfCardNowAndCashWhenYouCollect_WithCardSelected()
    {
        var html = await RenderAsync();

        var radios = Regex.Matches(html, "<input\\b[^>]*type=\"radio\"[^>]*>").Select(m => m.Value).ToList();
        Assert.Equal(2, radios.Count);
        Assert.All(radios, r => Assert.Contains("name=\"paymentMethod\"", r));
        Assert.Contains("value=\"Card\"", radios[0]);
        Assert.Matches("\\schecked[\\s>/]", radios[0]);
        Assert.Contains("value=\"Cash\"", radios[1]);
        Assert.DoesNotMatch("\\schecked[\\s>/]", radios[1]);

        var text = CheckoutOrderSummaryTests.Text(html);
        Assert.Contains("How would you like to pay?", text);
        Assert.Contains("Card now", text);
        Assert.Contains("Cash when you collect", text);
        Assert.Contains("Please bring $55.50 in cash when you collect your order.", text);
    }

    [Fact]
    public async Task Choice_HasOnePrimaryButton_WhoseTextFollowsTheChoice_AndGuardsAgainstDoubleSubmit()
    {
        var html = await RenderAsync();

        var button = Assert.Single(Regex.Matches(html, "<button\\b[^>]*>(.*?)</button>", RegexOptions.Singleline));
        Assert.Contains("type=\"submit\"", button.Value);
        Assert.Contains("x-bind:disabled=\"submitting\"", button.Value);
        var label = WebUtility.HtmlDecode(button.Groups[1].Value);
        // Both labels are rendered and the checked radio picks one through CSS, so it's right without JS.
        Assert.Contains("<span class=\"group-has-[#payment-cash:checked]:hidden\">Continue to payment</span>", label);
        Assert.Matches("<span class=\"hidden group-has-\\[#payment-cash:checked\\]:inline\"[^>]*>Place order</span>", label);
        Assert.Contains("'Placing order…'", label);
        Assert.Contains("id=\"payment-cash\"", html);
    }

    [Fact]
    public async Task ChoosingCard_ContinuesToPayment_WithoutPlacingAnOrder()
    {
        var orders = new RecordingOrderService(orderId: 7);
        var page = CreateReviewPage(PickupAddress(), orders, out _);

        var result = await page.OnPostPickupPaymentAsync(PaymentMethod.Card, CancellationToken.None);

        Assert.Equal("/Checkout/Payment", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(0, orders.CashOrdersPlaced);
    }

    [Fact]
    public async Task ChoosingCash_PlacesTheOrder_ClearsTheCart_AndShowsTheConfirmation()
    {
        var orders = new RecordingOrderService(orderId: 7);
        var page = CreateReviewPage(PickupAddress(), orders, out var cart);

        var result = await page.OnPostPickupPaymentAsync(PaymentMethod.Cash, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Checkout/Confirmation", redirect.PageName);
        Assert.Equal(7, redirect.RouteValues!["order"]);
        Assert.Equal(1, orders.CashOrdersPlaced);
        Assert.True(cart.Cleared);
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.Cash)]
    public async Task ShippingOrders_CantUseThePickupChoice(PaymentMethod paymentMethod)
    {
        var orders = new RecordingOrderService(orderId: 7);
        var address = PickupAddress();
        address.FulfillmentMethod = FulfillmentMethod.Shipping;
        var page = CreateReviewPage(address, orders, out _);

        var result = await page.OnPostPickupPaymentAsync(paymentMethod, CancellationToken.None);

        Assert.Equal("/Checkout/Review", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(0, orders.CashOrdersPlaced);
    }

    [Fact]
    public void ReviewPage_UsesTheChoiceForPickup_AndKeepsTheSingleContinueLinkForShipping()
    {
        var page = File.ReadAllText(Path.Combine(CheckoutPaymentFormTests.WebProjectDirectory(), "Pages", "Checkout", "Review.cshtml"));

        var form = Regex.Match(page, "<form\\b[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.Contains("method=\"post\"", form.Value);
        Assert.Contains("asp-page-handler=\"PickupPayment\"", form.Value);
        Assert.Contains("x-data=\"{ submitting: false }\"", form.Value);
        Assert.Contains("x-on:submit=\"submitting = true\"", form.Value);
        Assert.Contains("x-on:pageshow.window=\"submitting = false\"", form.Value);
        Assert.Equal("<partial name=\"_PickupPaymentChoice\" model=\"Model.Total\" />", form.Groups[1].Value.Trim());
        Assert.True(page.IndexOf("Model.Address.IsPickup", StringComparison.Ordinal) < form.Index);

        Assert.Matches("else\\s*\\{\\s*<a href=\"/checkout/payment\"[^>]*>\\s*Continue to payment\\s*</a>", page);
    }

    // The handler's paymentMethod parameter binds from the radios' name and values.
    [Fact]
    public void Radios_PostTheHandlersParameter()
    {
        var parameter = typeof(ReviewModel).GetMethod(nameof(ReviewModel.OnPostPickupPaymentAsync))!.GetParameters()[0];

        Assert.Equal("paymentMethod", parameter.Name);
        Assert.Equal(typeof(PaymentMethod), parameter.ParameterType);
    }

    private static CheckoutAddress PickupAddress() => new()
    {
        FulfillmentMethod = FulfillmentMethod.Pickup,
        Email = "shopper@example.com",
        FullName = "Sam Shopper",
    };

    private static ReviewModel CreateReviewPage(CheckoutAddress address, IOrderService orders, out RecordingCartService cart)
    {
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(address);
        cart = new RecordingCartService();
        return new ReviewModel(cart, orders, sessionStore)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider()),
        };
    }

    private static Task<string> RenderAsync() =>
        RazorViewToStringRendererTests.CreateRenderer().RenderAsync(PartialPath, 55.50m);

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class RecordingCartService : ICartService
    {
        public bool Cleared { get; private set; }

        public Task<CartSummary> GetSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> AddLineAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartOperationResult> UpdateLineQuantityAsync(string slug, int quantity, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLineAsync(string slug, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Cleared = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOrderService(int orderId) : IOrderService
    {
        public int CashOrdersPlaced { get; private set; }

        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken)
        {
            CashOrdersPlaced++;
            return Task.FromResult(CashOrderResult.Placed(orderId));
        }

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
