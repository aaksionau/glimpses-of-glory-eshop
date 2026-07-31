namespace GlimpsesOfGlory.Core.Store.Entities;

// Temporary entity proving the DB -> Core -> Web path end to end.
// Removed once real domain entities (Product, Order, ...) land in later slices.
public sealed class StoreStatus
{
    public int Id { get; set; }
    public required string Message { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
