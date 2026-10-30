using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Enums;

namespace GlimpsesOfGlory.Web.Dtos;

// What _CheckoutOrderSummary.cshtml shows on every checkout step.
// FollowsChoice is set on Details, where the shopper is still choosing how to receive the order:
// the summary then renders the shipping line and total for every method, starting on
// FulfillmentMethod, and the enclosing Alpine `method` (the selected radio) shows the matching one.
// Collapsible summaries start closed on small screens behind "Show order summary · $X";
// on large screens they are always open.
public sealed record CheckoutOrderSummary(
    CartSummary Cart,
    FulfillmentMethod FulfillmentMethod,
    bool FollowsChoice = false,
    bool Collapsible = true)
{
    // The methods whose totals the summary renders: just the chosen one, or all while choosing.
    public IReadOnlyList<FulfillmentMethod> Methods =>
        FollowsChoice ? Enum.GetValues<FulfillmentMethod>() : [FulfillmentMethod];

    public decimal ShippingCostFor(FulfillmentMethod method) => Cart.ShippingCostFor(method);

    public decimal TotalFor(FulfillmentMethod method) => Cart.Subtotal + Cart.ShippingCostFor(method);

    public decimal Total => TotalFor(FulfillmentMethod);
}
