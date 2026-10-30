namespace GlimpsesOfGlory.Abstractions.Enums;

public enum EmailResendResult
{
    Sent,
    NotFound,
    // The order's current status doesn't offer this email (see OrderTransitionPolicy).
    NotAllowed,
    // The email provider rejected or couldn't be reached; the failure is logged.
    Failed,
}
