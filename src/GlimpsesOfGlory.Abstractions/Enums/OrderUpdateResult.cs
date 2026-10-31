namespace GlimpsesOfGlory.Abstractions.Enums;

public enum OrderUpdateResult
{
    Updated,
    NotFound,
    // The order's current status doesn't allow the action (see OrderLifecycle).
    InvalidTransition,
}
