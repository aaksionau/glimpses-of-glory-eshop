using GlimpsesOfGlory.Core.ValueObjects;

namespace GlimpsesOfGlory.Core.Entities;

// Shared shape between Order and PendingCheckout - the checkout snapshot fields that
// exist regardless of whether payment has been confirmed yet. Lets both entities'
// EF configuration reuse the same precision/owned-type setup instead of repeating it.
internal interface ICheckoutHeader
{
    string Email { get; set; }
    ShippingAddress ShippingAddress { get; set; }
    decimal Subtotal { get; set; }
    decimal ShippingCost { get; set; }
    decimal Total { get; }
}
