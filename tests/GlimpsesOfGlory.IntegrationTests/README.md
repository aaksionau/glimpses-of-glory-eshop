# GlimpsesOfGlory.IntegrationTests

Tests that run against a real Postgres, started by Testcontainers, so **Docker must be running**.
One container serves the whole run; its schema is built once by the app's real migrations
(`Database.MigrateAsync()`), and every test gets a fresh copy of that database.

## Here or in the SQLite unit tests?

Write the test in `GlimpsesOfGlory.UnitTests` (in-memory SQLite via `SqliteTestDatabase`) by
default: it is fast and needs no Docker.

Put it here only when the behavior under test depends on Postgres itself, for example:

- row locking and concurrency (two contexts racing a status-guarded `ExecuteUpdate`);
- transaction semantics (a rollback undoing writes made by several statements);
- how a LINQ expression is translated to SQL (`ToLower().Contains`, date functions, ...);
- `DateTimeOffset` / `timestamptz` storage, comparison and ordering — the SQLite tests store
  these as binary, so they can't tell you how Postgres sorts them;
- the migrations themselves.

If the SQLite tests would give the same answer as Postgres, the test belongs there.
