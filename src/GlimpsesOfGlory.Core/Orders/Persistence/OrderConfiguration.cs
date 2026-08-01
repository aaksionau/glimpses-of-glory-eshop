using GlimpsesOfGlory.Core.Orders.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Orders.Persistence;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    // Named explicitly (rather than left to EF's naming convention) so
    // DbUpdateExceptionExtensions.IsDuplicateStripePaymentIntentId can match it reliably -
    // a convention-generated name would silently drift if the table/property is renamed.
    public const string StripePaymentIntentIdIndexName = "IX_Orders_StripePaymentIntentId";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(o => o.StripePaymentIntentId)
            .IsUnique()
            .HasDatabaseName(StripePaymentIntentIdIndexName);
        builder.ConfigureCheckoutHeader();

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ConfigureCheckoutLine();
    }
}
