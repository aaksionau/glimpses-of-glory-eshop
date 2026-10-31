# GlimpsesOfGlory.IntegrationTests

Tests that run against a real Postgres, started by Testcontainers, so **Docker must be running**.
One container serves the whole run; its schema is built once by the app's real migrations
(`Database.MigrateAsync()`), and every test gets a fresh copy of that database.

## What belongs here

Every test that needs a database or the HTTP pipeline:

- services whose queries run through `AppDbContext` (order, checkout, cart restore, dashboard,
  abandoned checkouts, ...) and PageModel tests that drive such a service;
- the SQL form of rules (`OrderRule.OnOrders` must agree with its in-memory form);
- row locking and concurrency (two contexts racing a status-guarded `ExecuteUpdate`);
- transaction semantics (a rollback undoing writes made by several statements);
- `DateTimeOffset` / `timestamptz` storage, comparison and ordering;
- the migrations themselves;
- requests through the real pipeline (see below).

Tests that need no database belong in `GlimpsesOfGlory.UnitTests`. There is deliberately no
SQLite or in-memory provider anywhere, so query tests always run on the provider production uses.

## Writing a test

Join the shared container with `[Collection(PostgresCollectionDefinition.Name)]`, take
`PostgresFixture` in the constructor, and get a fresh database per test in `IAsyncLifetime`:

```csharp
private TestDatabase database = null!;

public async Task InitializeAsync() => database = await postgres.CreateDatabaseAsync();

public async Task DisposeAsync() => await database.DisposeAsync();
```

`database.CreateDb()` returns a new `AppDbContext` (and connection) each call. Raw SQL must use
Postgres' quoted identifiers (`UPDATE "Products" SET "StockQuantity" = ...`).

Test doubles shared with the unit tests live in `tests/TestSupport/` (namespace
`GlimpsesOfGlory.TestSupport`); doubles only this project uses (`FakePaymentGateway`,
`FixedTimeProvider`, `NullEmailSender`, `RecordingEmailSender`, `RecordingLogger`, ...) live here.

## HTTP tests through the real pipeline

`EshopWebApplicationFactory` boots `Program.cs` (a `WebApplicationFactory<Program>`) against one
test's database, with `IPaymentGateway` and `IEmailSender` replaced by in-memory fakes
(`FakePaymentGateway`, `RecordingEmailSender`). Use it for tests that need routing, middleware
or auth conventions, such as `EndpointTests`.
