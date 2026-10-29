namespace GlimpsesOfGlory.Abstractions.Enums;

// Persisted as an int column - keep the explicit values stable and append new ones.
public enum OrderStatus
{
    New = 0,
    Shipped = 1,
    Cancelled = 2,

    // Final step for pickup orders only: the buyer has collected an order that was ready for pickup.
    PickedUp = 3,
}
