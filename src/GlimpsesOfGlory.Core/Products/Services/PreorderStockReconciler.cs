namespace GlimpsesOfGlory.Core.Products.Services;

public static class PreorderStockReconciler
{
    public static int Reconcile(int quantityReceived, int preorderedQuantity) =>
        Math.Max(0, quantityReceived - preorderedQuantity);
}
