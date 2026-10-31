using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Dtos;
using GlimpsesOfGlory.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.Web.Pages.Checkout;

public class ReviewModel(ICartService cartService, IOrderService orderService, CheckoutSessionStore checkoutSessionStore) : PageModel
{
    public CartSummary Cart { get; private set; } = null!;

    public CheckoutAddress Address { get; private set; } = null!;

    // Review is where the shopper checks the order, so its summary is never collapsed.
    public CheckoutOrderSummary Summary => new(Cart, Address.FulfillmentMethod, Collapsible: false);

    public decimal Total => Summary.Total;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        var address = checkoutSessionStore.GetAddress();
        if (CheckoutStepGuard.ShouldRedirect(cart, address, out var redirect))
        {
            return redirect;
        }

        Cart = cart;
        Address = address;
        return Page();
    }

    // A pickup shopper's payment choice: card continues to the Payment step; cash has no payment
    // step, so the order is placed right here.
    public async Task<IActionResult> OnPostPickupPaymentAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken)
    {
        var address = checkoutSessionStore.GetAddress();
        if (CheckoutStepGuard.ShouldRedirect(address, out var redirect))
        {
            return redirect;
        }

        // Cash is only offered for local pickup.
        if (!address.IsPickup)
        {
            return RedirectToPage("/Checkout/Review");
        }

        if (paymentMethod != PaymentMethod.Cash)
        {
            return RedirectToPage("/Checkout/Payment");
        }

        var result = await orderService.PlaceCashOrderAsync(address.ToShippingAddressInfo(), cancellationToken);
        if (result.OrderId is not { } orderId)
        {
            CartLineErrorsTempData.ReportUnavailable(TempData, result.UnavailableLines);
            return RedirectToPage("/Cart/Index");
        }

        await cartService.ClearAsync(cancellationToken);
        checkoutSessionStore.Clear();
        checkoutSessionStore.SavePlacedOrderId(orderId);
        return RedirectToPage("/Checkout/Confirmation", new { order = orderId });
    }
}
