namespace GlimpsesOfGlory.Abstractions.Dtos;

// Narrows the admin orders list across every tab. Search matches an order number ("42" or "#42")
// or part of the buyer's email or name, case-insensitively; a leading "#" searches order numbers only.
// PreorderOnly keeps orders with at least one preorder line.
public sealed record AdminOrderFilter(string? Search = null, bool PreorderOnly = false)
{
    public static AdminOrderFilter None { get; } = new();
}
