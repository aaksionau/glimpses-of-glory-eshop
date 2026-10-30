namespace GlimpsesOfGlory.Abstractions.Enums;

// The carrier a shipped order went out with. Only USPS is supported for tracking links (decided on
// issue #54); Other is stored and shown as plain text. Values are explicit because they're persisted.
public enum ShippingCarrier
{
    Usps = 1,
    Other = 2,
}
