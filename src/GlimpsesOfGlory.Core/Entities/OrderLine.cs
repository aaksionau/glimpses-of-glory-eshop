namespace GlimpsesOfGlory.Core.Entities;

public sealed class OrderLine : ICheckoutLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public bool IsPreorder { get; set; }
    public DateOnly? ExpectedAvailabilityDate { get; set; }
}
