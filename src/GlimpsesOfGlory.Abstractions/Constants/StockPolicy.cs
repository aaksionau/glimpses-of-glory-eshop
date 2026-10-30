namespace GlimpsesOfGlory.Abstractions.Constants;

public static class StockPolicy
{
    // A visible, in-stock-tracked product at or below this many units is flagged as low stock on
    // the admin dashboard. Preorder products are never low stock - they have no stock ceiling.
    public const int LowStockThreshold = 2;
}
