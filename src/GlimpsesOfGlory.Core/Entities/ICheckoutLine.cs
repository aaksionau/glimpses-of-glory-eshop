namespace GlimpsesOfGlory.Core.Entities;

// Shared shape between OrderLine and PendingCheckoutLine.
public interface ICheckoutLine
{
    int ProductId { get; set; }
    string ProductName { get; set; }
    decimal UnitPrice { get; set; }
    int Quantity { get; set; }
    bool IsPreorder { get; set; }
    DateOnly? ExpectedAvailabilityDate { get; set; }
}
