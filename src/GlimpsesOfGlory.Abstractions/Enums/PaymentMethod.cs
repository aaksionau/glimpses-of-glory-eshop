namespace GlimpsesOfGlory.Abstractions.Enums;

public enum PaymentMethod
{
    Card,
    // Paid in person when collecting the order - only offered for FulfillmentMethod.Pickup.
    Cash,
}
