using GlimpsesOfGlory.Domain;
using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StoreStatus> StoreStatuses => Set<StoreStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoreStatus>().HasData(new StoreStatus
        {
            Id = 1,
            Message = "Glimpses of Glory is under construction.",
            UpdatedAtUtc = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
