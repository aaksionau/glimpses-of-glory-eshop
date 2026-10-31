namespace GlimpsesOfGlory.Web.Formatting;

public static class CheckoutMessages
{
    public const string ItemsUnavailable = "Sorry, one or more items in your cart are no longer available in the requested quantity.";

    // Shown under a cart line checkout couldn't reserve.
    public static string LineUnavailable(int availableQuantity) => availableQuantity > 0
        ? $"Only {availableQuantity} left. Lower the quantity to continue."
        : "No longer available. Remove it to continue.";

    public const string PaymentFormUnavailable = "Payment form couldn't load. Refresh or try again later.";

    public const string PaymentFailed = "Your payment could not be processed. Please try again.";

    public const string PaymentProcessing = "Processing…";

    public const string RestoreLinkExpired = "That cart link has expired; your current cart is below.";

    public const string RestoredItemsUnavailable = "Some items from your previous cart are no longer available.";
}
