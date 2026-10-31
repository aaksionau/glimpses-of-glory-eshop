namespace GlimpsesOfGlory.Abstractions.Enums;

public enum CartRestoreStatus
{
    Restored,
    // The token matches no Pending Checkout: never issued, or the checkout has since been paid or removed.
    Expired,
}
