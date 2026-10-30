namespace GlimpsesOfGlory.Abstractions.Dtos;

// Narrows the admin orders list across every tab. Search matches an order number ("42" or "#42")
// or part of the buyer's email or name, case-insensitively; a leading "#" searches order numbers only.
// PreorderOnly keeps orders with at least one preorder line. ToFulfillOnly keeps New orders, and
// UnpaidCashOnly keeps cash orders still to be paid that aren't cancelled - the same rules as the
// matching admin dashboard numbers.
public sealed record AdminOrderFilter(string? Search = null, bool PreorderOnly = false, bool ToFulfillOnly = false, bool UnpaidCashOnly = false)
{
    public static AdminOrderFilter None { get; } = new();
}
