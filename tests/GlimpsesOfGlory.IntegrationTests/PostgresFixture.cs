using GlimpsesOfGlory.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GlimpsesOfGlory.IntegrationTests;

// One Postgres container for the whole test run. The schema is built once, by the app's real
// migrations, into a template database; each test then gets its own copy of that template, so
// tests never see each other's rows and a broken migration fails every test.
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "eshop_template";

    // Same major version as docker-compose.yml.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await ExecuteAsync($"CREATE DATABASE \"{TemplateDatabase}\"");

        await using (var db = new AppDbContext(OptionsBuilder(ConnectionStringFor(TemplateDatabase)).Options))
        {
            await db.Database.MigrateAsync();
        }

        // CREATE DATABASE ... TEMPLATE refuses while anything is still connected to the template.
        NpgsqlConnection.ClearAllPools();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"");
        return new TestDatabase(ConnectionStringFor(name), () => ExecuteAsync($"DROP DATABASE \"{name}\" WITH (FORCE)"));
    }

    // The same provider setup as Program.cs.
    internal static DbContextOptionsBuilder<AppDbContext> OptionsBuilder(string connectionString) =>
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

// Every test class joins this collection, so they share the one container (and run one at a time).
[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
