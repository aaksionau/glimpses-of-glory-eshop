namespace GlimpsesOfGlory.Abstractions.Dtos;

// What the abandoned-checkout reminder email shows: item names and quantities only, no prices.
public sealed record AbandonedCheckoutReminderView(
    string Email,
    IReadOnlyList<AbandonedCheckoutLine> Lines,
    string RestoreUrl);
