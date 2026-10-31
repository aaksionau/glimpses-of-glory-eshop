namespace GlimpsesOfGlory.Abstractions.Dtos;

// When an order reached each lifecycle step. A step is null when it hasn't happened - or happened
// before step timestamps were recorded. (Named apart from Web's OrderTimeline, which turns these
// into the steps the admin order page shows.)
public sealed record OrderTimestamps
{
    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    public DateTimeOffset? ShippedAt { get; init; }

    public DateTimeOffset? ReadyForPickupAt { get; init; }

    public DateTimeOffset? PickedUpAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }
}
