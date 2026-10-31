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
