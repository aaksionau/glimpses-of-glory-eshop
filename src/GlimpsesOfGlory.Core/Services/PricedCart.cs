using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Services;

// The cart as it's about to be charged. Lines are priced from the catalogue; Cart is the session
// cart with its lines repriced the same way, for showing what's charged. Both keep the cart's line order.
public sealed record PricedCart(List<PendingCheckoutLine> Lines, CartSummary Cart, FulfillmentMethod FulfillmentMethod)
{
    public decimal Subtotal => Cart.Subtotal;
    public decimal ShippingCost => Cart.ShippingCostFor(FulfillmentMethod);
    public decimal Total => Cart.TotalFor(FulfillmentMethod);
}
