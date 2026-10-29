using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Helpers;

// One badge vocabulary for order and payment status across the admin pages.
public static class OrderBadges
{
    private const string Amber = "bg-amber-100 text-amber-800 border-amber-300";
    private const string Blue = "bg-blue-100 text-blue-800 border-blue-300";
    private const string Green = "bg-green-100 text-green-800 border-green-300";
    private const string Gray = "bg-gray-100 text-gray-600 border-gray-300";
    private const string RedOutline = "bg-white text-red-700 border-red-600";
    private const string RedSolid = "bg-red-600 text-white border-red-600";
    private const string Neutral = "bg-white text-gray-700 border-gray-300";

    // New = amber, Shipped/Ready for pickup = blue, Picked up (completed) = green, Cancelled = gray.
    public static Badge Status(OrderStatus status, FulfillmentMethod fulfillmentMethod) =>
        new(FulfillmentLabels.Status(status, fulfillmentMethod), status switch
        {
            OrderStatus.New => Amber,
            OrderStatus.Shipped => Blue,
            OrderStatus.PickedUp => Green,
            _ => Gray,
        });

    public static Badge Payment(bool isPaid) =>
        isPaid ? new("Paid", Green) : new("Unpaid", RedOutline);

    public static Badge RefundRequired { get; } = new("Refund required", RedSolid);

    public static Badge Preorder { get; } = Tag("Preorder");

    public static Badge Pickup { get; } = Tag("Pickup");

    public static Badge Tag(string text) => new(text, Neutral);
}
