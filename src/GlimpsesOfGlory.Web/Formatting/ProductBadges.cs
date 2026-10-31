using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.Web.Formatting;

// Badges for the admin product pages; Preorder reuses OrderBadges.Preorder.
public static class ProductBadges
{
    private const string Green = "bg-green-100 text-green-800 border-green-300";
    private const string Gray = "bg-gray-100 text-gray-600 border-gray-300";
    private const string Neutral = "bg-white text-gray-700 border-gray-300";

    public static Badge Visibility(bool isActive) =>
        isActive ? new("Visible", Green) : new("Hidden", Gray);

    public static Badge MainPhoto { get; } = new("Main", Neutral);
}
