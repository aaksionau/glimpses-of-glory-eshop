using GlimpsesOfGlory.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.EntityConfigurations;

internal sealed class ShippingSettingConfiguration : IEntityTypeConfiguration<ShippingSetting>
{
    public const int DefaultId = 1;

    public void Configure(EntityTypeBuilder<ShippingSetting> builder)
    {
        builder.Property(s => s.FirstItemAmount).HasPrecision(10, 2);
        builder.Property(s => s.AdditionalItemAmount).HasPrecision(10, 2);

        // Exactly one row: the store default, editable by the store owner via /admin/shipping.
        // $5 + $1 per extra item keeps the old tiers' prices for 1 and 5 items.
        builder.HasData(new ShippingSetting { Id = DefaultId, FirstItemAmount = 5.00m, AdditionalItemAmount = 1.00m });
    }
}
