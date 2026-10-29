using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class FulfillmentLabelsTests
{
    [Theory]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, "New")]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, "Shipped")]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, "Cancelled")]
    [InlineData(OrderStatus.New, FulfillmentMethod.Pickup, "New")]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Pickup, "Ready for pickup")]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, "Picked up")]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, "Cancelled")]
    public void Status_WordsTheStatusForTheFulfillmentMethod(OrderStatus status, FulfillmentMethod fulfillmentMethod, string expected)
    {
        Assert.Equal(expected, FulfillmentLabels.Status(status, fulfillmentMethod));
    }
}
