namespace GlimpsesOfGlory.Abstractions.Enums;

// The status tabs on the admin orders list. Tabs can overlap: an unpaid cash order that has
// shipped shows under both Needs action and Shipped.
public enum AdminOrderTab
{
    // New orders, plus unpaid orders that aren't cancelled (see OrderLifecycle.NeedsAction).
    NeedsAction,

    // Shipped or ready for pickup, and picked up.
    Shipped,

    Cancelled,

    All,
}
