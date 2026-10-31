namespace GlimpsesOfGlory.Abstractions.Dtos;

// What the admin can do to an order right now, as decided by the order lifecycle. Set by name
// (required init properties, not positional) so two flags can't be swapped silently.
public sealed record AdminOrderActions
{
    public required bool CanMarkShipped { get; init; }

    public required bool CanMarkReadyForPickup { get; init; }

    public required bool CanMarkPickedUp { get; init; }

    public required bool CanMarkPaid { get; init; }

    public required bool CanMarkCancelled { get; init; }

    // A cancelled, paid card order whose money has to be refunded by hand.
    public required bool NeedsRefund { get; init; }

    public required bool CanResendConfirmation { get; init; }

    public required bool CanResendShippedEmail { get; init; }

    public required bool CanResendReadyForPickupEmail { get; init; }
}
