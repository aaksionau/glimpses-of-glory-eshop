using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class AdminSectionsTests
{
    [Theory]
    [InlineData("/admin", AdminSection.Dashboard)]
    [InlineData("/Admin/", AdminSection.Dashboard)]
    [InlineData("/admin/index", AdminSection.Dashboard)]
    [InlineData("/admin/orders", AdminSection.Orders)]
    [InlineData("/admin/orders/42", AdminSection.Orders)]
    [InlineData("/admin/products", AdminSection.Products)]
    [InlineData("/admin/products/new", AdminSection.Products)]
    [InlineData("/admin/products/7/edit", AdminSection.Products)]
    [InlineData("/admin/shipping", AdminSection.Shipping)]
    [InlineData("/admin/logout", AdminSection.None)]
    [InlineData("/products", AdminSection.None)]
    [InlineData("/administrator", AdminSection.None)]
    [InlineData("", AdminSection.None)]
    [InlineData(null, AdminSection.None)]
    public void FromPath_MapsRequestPathToSidebarSection(string? path, AdminSection expected)
    {
        Assert.Equal(expected, AdminSections.FromPath(path));
    }
}
