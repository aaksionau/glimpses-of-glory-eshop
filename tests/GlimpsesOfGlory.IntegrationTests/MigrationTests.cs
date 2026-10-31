using Microsoft.EntityFrameworkCore;

namespace GlimpsesOfGlory.IntegrationTests;

// The fixture builds the schema with MigrateAsync, so a migration that fails on Postgres already
// fails the whole suite; this pins down that every migration really was applied.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EveryMigration_IsApplied()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDb();

        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }
}
