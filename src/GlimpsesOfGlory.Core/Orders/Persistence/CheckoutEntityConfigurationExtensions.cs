using GlimpsesOfGlory.Core.Orders.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Orders.Persistence;

// Shared EF configuration for the fields Order/PendingCheckout and OrderLine/PendingCheckoutLine
// have in common, so the two configuration classes below don't repeat the same setup.
internal static class CheckoutEntityConfigurationExtensions
{
    public static void ConfigureCheckoutHeader<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ICheckoutHeader
    {
        builder.Property(e => e.Subtotal).HasPrecision(10, 2);
        builder.Property(e => e.ShippingCost).HasPrecision(10, 2);
        builder.Property(e => e.Total).HasPrecision(10, 2);
        builder.OwnsOne(e => e.ShippingAddress);
    }

    public static void ConfigureCheckoutLine<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ICheckoutLine
    {
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2);
    }
}
