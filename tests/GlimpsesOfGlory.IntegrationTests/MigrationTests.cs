using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// The fixture builds the schema with MigrateAsync, so a migration that fails on Postgres already
// fails the whole suite; this pins down that every migration really was applied.
[Collection(PostgresCollection.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EveryMigration_IsApplied_AndNoneArePending()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDb();

        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
