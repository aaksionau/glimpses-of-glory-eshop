using GlimpsesOfGlory.Abstractions.Dtos;
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

    public decimal ShippingCost { get; private set; }

    public decimal Total => Cart.Subtotal + ShippingCost;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = await cartService.GetSummaryAsync(cancellationToken);
        if (cart.Lines.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

        var address = checkoutSessionStore.GetAddress();
        if (address is null)
        {
            return RedirectToPage("/Checkout/Index");
        }

        Cart = cart;
        Address = address;
        ShippingCost = await cartService.GetShippingCostAsync(address.FulfillmentMethod, cancellationToken);
        return Page();
    }

    // Pay-cash-at-pickup: there's no payment step, so the order is placed right here.
    public async Task<IActionResult> OnPostPlaceCashOrderAsync(CancellationToken cancellationToken)
    {
        var address = checkoutSessionStore.GetAddress();
        if (address is null)
        {
            return RedirectToPage("/Checkout/Index");
        }

        // Cash is only offered for local pickup.
        if (!address.IsPickup)
        {
            return RedirectToPage("/Checkout/Review");
        }

        var orderId = await orderService.PlaceCashOrderAsync(address.ToShippingAddressInfo(), cancellationToken);
        if (orderId is null)
        {
            TempData["Error"] = CheckoutMessages.ItemsUnavailable;
            return RedirectToPage("/Cart/Index");
        }

        await cartService.ClearAsync(cancellationToken);
        checkoutSessionStore.Clear();
        checkoutSessionStore.SavePlacedOrderId(orderId.Value);
        return RedirectToPage("/Checkout/Confirmation", new { order = orderId.Value });
    }
}
