using GlimpsesOfGlory.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GlimpsesOfGlory.UnitTests;

// An in-memory SQLite database holding the app's schema, for services whose queries need a
// relational provider (ExecuteUpdate, aggregates, translated filters). Each CreateDb() call
// returns a fresh context over the same database.
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        connection.Open();
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDb(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>()
            .AddInterceptors(interceptors)
            .Options);

    public void Dispose() => connection.Dispose();

    // SQLite can't ORDER BY or compare a DateTimeOffset (production runs on Postgres, which can), so
    // the test model stores them as sortable binary values instead.
    private sealed class SqliteDateTimeOffsetCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
