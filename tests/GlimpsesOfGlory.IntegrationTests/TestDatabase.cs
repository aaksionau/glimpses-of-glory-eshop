using GlimpsesOfGlory.Core;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GlimpsesOfGlory.IntegrationTests;

// A migrated database owned by one test. Each CreateDb() call returns a fresh context (and so its
// own connection) over it; disposing drops the database.
public sealed class TestDatabase(string connectionString, Func<Task> drop) : IAsyncDisposable
{
    public AppDbContext CreateDb(params IInterceptor[] interceptors) =>
        new(PostgresFixture.OptionsBuilder(connectionString).AddInterceptors(interceptors).Options);

    // DROP ... WITH (FORCE) closes any connections still pooled against it.
    public async ValueTask DisposeAsync() => await drop();
}
