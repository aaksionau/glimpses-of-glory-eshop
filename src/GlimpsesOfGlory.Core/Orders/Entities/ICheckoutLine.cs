namespace GlimpsesOfGlory.Core.Orders.Entities;

// Shared shape between OrderLine and PendingCheckoutLine.
internal interface ICheckoutLine
{
    int ProductId { get; set; }
    string ProductName { get; set; }
    decimal UnitPrice { get; set; }
    int Quantity { get; set; }
}
