namespace GlimpsesOfGlory.Core.Orders.Entities;

public sealed class PendingCheckoutLine : ICheckoutLine
{
    public int Id { get; set; }
    public int PendingCheckoutId { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
