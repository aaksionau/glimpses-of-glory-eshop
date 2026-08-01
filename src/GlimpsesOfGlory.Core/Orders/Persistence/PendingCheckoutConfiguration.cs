using GlimpsesOfGlory.Core.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Orders.Persistence;

internal sealed class PendingCheckoutConfiguration : IEntityTypeConfiguration<PendingCheckout>
{
    public void Configure(EntityTypeBuilder<PendingCheckout> builder)
    {
        builder.HasIndex(p => p.StripePaymentIntentId).IsUnique();
        builder.ConfigureCheckoutHeader();

        builder.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PendingCheckoutId);
    }
}

internal sealed class PendingCheckoutLineConfiguration : IEntityTypeConfiguration<PendingCheckoutLine>
{
    public void Configure(EntityTypeBuilder<PendingCheckoutLine> builder)
    {
        builder.ConfigureCheckoutLine();
    }
}
