using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Core.Entities;

namespace GlimpsesOfGlory.Core.Extensions;

// The customer-facing email models built from an order (with its lines loaded). Shared by
// checkout, marking shipped, and the admin resend actions so every send renders the same email.
internal static class OrderEmailViewExtensions
{
    public static OrderConfirmationView ToConfirmationView(this Order order) => new(
        order.Id,
        order.FulfillmentMethod,
        order.IsPaid,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(ToConfirmationLine).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.CreatedAt);

    public static OrderShippedView ToShippedView(this Order order) => new(
        order.Id,
        order.FulfillmentMethod,
        order.IsPaid,
        order.ShippingAddress.ToInfo(order.Email),
        order.Lines.Select(ToConfirmationLine).ToList(),
        order.Total,
        order.TrackingNumber);

    public static OrderConfirmationLine ToConfirmationLine(this OrderLine line) =>
        new(line.ProductName, line.UnitPrice, line.Quantity, line.IsPreorder, line.ExpectedAvailabilityDate);
}
