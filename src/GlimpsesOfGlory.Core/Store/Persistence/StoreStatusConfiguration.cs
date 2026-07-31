using GlimpsesOfGlory.Core.Store.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlimpsesOfGlory.Core.Store.Persistence;

internal sealed class StoreStatusConfiguration : IEntityTypeConfiguration<StoreStatus>
{
    public void Configure(EntityTypeBuilder<StoreStatus> builder)
    {
        builder.HasData(new StoreStatus
        {
            Id = 1,
            Message = "Glimpses of Glory is under construction.",
            UpdatedAtUtc = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
