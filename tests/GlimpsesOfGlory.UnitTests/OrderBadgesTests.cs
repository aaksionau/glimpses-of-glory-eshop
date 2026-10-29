using GlimpsesOfGlory.Abstractions.Enums;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class OrderBadgesTests
{
    [Theory]
    [InlineData(OrderStatus.New, FulfillmentMethod.Shipping, "New", "amber")]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Shipping, "Shipped", "blue")]
    [InlineData(OrderStatus.Shipped, FulfillmentMethod.Pickup, "Ready for pickup", "blue")]
    [InlineData(OrderStatus.PickedUp, FulfillmentMethod.Pickup, "Picked up", "green")]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Shipping, "Cancelled", "gray")]
    [InlineData(OrderStatus.Cancelled, FulfillmentMethod.Pickup, "Cancelled", "gray")]
    public void Status_UsesTheColourForEachStatus(OrderStatus status, FulfillmentMethod fulfillmentMethod, string expectedText, string expectedColour)
    {
        var badge = OrderBadges.Status(status, fulfillmentMethod);

        Assert.Equal(expectedText, badge.Text);
        Assert.Contains($"bg-{expectedColour}-100", badge.CssClass);
    }

    [Fact]
    public void Payment_PaidIsGreen()
    {
        var badge = OrderBadges.Payment(isPaid: true);

        Assert.Equal("Paid", badge.Text);
        Assert.Contains("bg-green-100", badge.CssClass);
    }

    [Fact]
    public void Payment_UnpaidIsRedOutline()
    {
        var badge = OrderBadges.Payment(isPaid: false);

        Assert.Equal("Unpaid", badge.Text);
        Assert.Contains("border-red-600", badge.CssClass);
        Assert.Contains("bg-white", badge.CssClass);
    }

    [Fact]
    public void PreorderAndPickup_AreNeutralTags()
    {
        Assert.Equal("Preorder", OrderBadges.Preorder.Text);
        Assert.Equal("Pickup", OrderBadges.Pickup.Text);
        Assert.Equal(OrderBadges.Tag("x").CssClass, OrderBadges.Preorder.CssClass);
        Assert.Equal(OrderBadges.Tag("x").CssClass, OrderBadges.Pickup.CssClass);
    }
}
