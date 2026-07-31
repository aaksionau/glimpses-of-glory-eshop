using GlimpsesOfGlory.Core.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Orders.Persistence;

internal sealed class PendingCheckoutConfiguration : IEntityTypeConfiguration<PendingCheckout>
{
    public void Configure(EntityTypeBuilder<PendingCheckout> builder)
    {
        builder.HasIndex(p => p.StripePaymentIntentId).IsUnique();
        builder.Property(p => p.Subtotal).HasPrecision(10, 2);
        builder.Property(p => p.ShippingCost).HasPrecision(10, 2);
        builder.Property(p => p.Total).HasPrecision(10, 2);

        builder.OwnsOne(p => p.ShippingAddress);

        builder.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PendingCheckoutId);
    }
}

internal sealed class PendingCheckoutLineConfiguration : IEntityTypeConfiguration<PendingCheckoutLine>
{
    public void Configure(EntityTypeBuilder<PendingCheckoutLine> builder)
    {
        builder.Property(l => l.UnitPrice).HasPrecision(10, 2);
    }
}
