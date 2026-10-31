using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

public class AdminSectionsTests
{
    [Theory]
    [InlineData("/admin", AdminSection.Dashboard)]
    [InlineData("/admin/orders/12", AdminSection.Orders)]
    [InlineData("/admin/abandoned-checkouts", AdminSection.AbandonedCheckouts)]
    [InlineData("/Admin/Abandoned-Checkouts/", AdminSection.AbandonedCheckouts)]
    [InlineData("/cart", AdminSection.None)]
    public void FromPath_MapsToSection(string path, AdminSection expected)
    {
        Assert.Equal(expected, AdminSections.FromPath(path));
    }

    [Fact]
    public void AbandonedCheckouts_ComesRightAfterOrders()
    {
        var sections = AdminSections.Links.Select(l => l.Section).ToList();

        Assert.Equal(sections.IndexOf(AdminSection.Orders) + 1, sections.IndexOf(AdminSection.AbandonedCheckouts));
        Assert.Equal("/admin/abandoned-checkouts", AdminSections.Links.Single(l => l.Section == AdminSection.AbandonedCheckouts).Href);
    }
}
