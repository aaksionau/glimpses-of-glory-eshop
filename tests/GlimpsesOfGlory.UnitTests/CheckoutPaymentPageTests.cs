using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.UnitTests;

public class CheckoutPaymentPageTests
{
    // Two items at $20 + one at $15.50 = $55.50 subtotal; shipping to an address costs $8.
    private static readonly CartSummary Cart = new(
        [
            new CartLineView("sunrise", "Sunrise over the lake", "sunrise.jpg", 20m, 2, false, null),
            new CartLineView("harbour", "Harbour at dusk", null, 15.50m, 1, false, null),
        ],
        Subtotal: 55.50m,
        ShippingCost: 8m);

    [Fact]
    public async Task PaymentPage_SummarisesAndShowsTheAmountCharged_EvenWhenAPriceChangedSinceAddingToCart()
    {
        // The catalogue price of "sunrise" went from $20 to $25 after it was added to the cart:
        // 2 × $25 + $15.50 = $65.50 subtotal, + $8 shipping = $73.50 charged.
        var pricedCart = Cart with
        {
            Lines = [Cart.Lines[0] with { UnitPrice = 25m }, Cart.Lines[1]],
            Subtotal = 65.50m,
        };

        var page = await CreatePaymentPageAsync(new CheckoutPaymentSetup("pi_1", "secret_1", pricedCart));

        Assert.Equal(FulfillmentMethod.Shipping, page.Summary.FulfillmentMethod);
        Assert.Same(pricedCart, page.Summary.Cart);
        Assert.Equal(73.50m, page.Summary.Total);
        Assert.Equal(73.50m, page.AmountDue);
    }

    // The Payment page after OnGetAsync for a shipping order, with the PaymentIntent set up as given.
    private static async Task<PaymentModel> CreatePaymentPageAsync(CheckoutPaymentSetup setup)
    {
        var address = new CheckoutAddress
        {
            FulfillmentMethod = FulfillmentMethod.Shipping,
            Email = "shopper@example.com",
            FullName = "Sam Shopper",
            AddressLine1 = "1 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "United States",
        };
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        sessionStore.SaveAddress(address);
        var page = new PaymentModel(
            new StubCartService(Cart),
            sessionStore,
            new StubOrderService(setup),
            Options.Create(new StripeOptions { PublishableKey = "pk_test" }))
        {
            PageContext = new PageContext { HttpContext = httpContext },
        };

        await page.OnGetAsync(CancellationToken.None);
        return page;
    }

    private sealed class StubOrderService(CheckoutPaymentSetup setup) : IOrderService
    {
        public Task<CheckoutPaymentResult> CreatePaymentIntentAsync(FulfillmentMethod fulfillmentMethod, ShippingAddressInfo address, string? existingPaymentIntentId, CancellationToken cancellationToken) =>
            Task.FromResult(CheckoutPaymentResult.Ready(setup));

        public Task ConfirmPaymentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CashOrderResult> PlaceCashOrderAsync(ShippingAddressInfo address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderConfirmationView?> GetOrderConfirmationAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
