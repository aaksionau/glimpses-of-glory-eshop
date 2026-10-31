namespace GlimpsesOfGlory.Abstractions.Enums;

// Modeled on EmailResendResult, for the one-time abandoned-checkout reminder.
public enum ReminderSendResult
{
    Sent,
    // A reminder already went out for this checkout; nothing was sent.
    AlreadyReminded,
    NotFound,
    // The email provider rejected or couldn't be reached; the failure is logged and nothing is stamped.
    Failed,
}
