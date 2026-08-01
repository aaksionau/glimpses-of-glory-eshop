using GlimpsesOfGlory.Core.Shipping.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Shipping.Persistence;

internal sealed class ShippingTierSettingConfiguration : IEntityTypeConfiguration<ShippingTierSetting>
{
    public void Configure(EntityTypeBuilder<ShippingTierSetting> builder)
    {
        builder.Property(t => t.Amount).HasPrecision(10, 2);

        // Exactly two flat-rate tiers, editable by the store owner via /admin/shipping.
        // Values match the previous appsettings.json defaults.
        builder.HasData(
            new ShippingTierSetting { Id = 1, MinQuantity = 1, Amount = 5.00m },
            new ShippingTierSetting { Id = 2, MinQuantity = 5, Amount = 9.00m });
    }
}
