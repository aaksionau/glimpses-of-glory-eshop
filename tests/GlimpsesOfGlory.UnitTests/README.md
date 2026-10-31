# GlimpsesOfGlory.UnitTests

Fast tests that need **no database and no Docker**: pure logic (`ShippingCalculator`,
`OrderLifecycle` rules in memory, CSV export), PageModels driven with stubbed services,
Razor email/view rendering, the Stripe and Resend adapters, and the project boundary rules — Web→Core,
Web→Infrastructure, Core↛Infrastructure, Infrastructure↛Core (`ProjectBoundaryTests`).

## Here or in the integration tests?

If the test needs an `AppDbContext` — a service whose queries run against the database, a
migration, or the real HTTP pipeline — it belongs in `GlimpsesOfGlory.IntegrationTests`, which
runs it against real Postgres. There is no in-memory or SQLite database here, so a passing
query test always means it passes on the provider production uses.

## Shared test doubles

Fakes and helpers used by both test projects (`FakeCartStore`, `FakeInventoryStore`,
`InMemorySession`, `NullTempDataProvider`, `TestRazorRenderer`, …) live once in
`tests/TestSupport/` and are compiled into both projects (see the `.csproj`), under the
`GlimpsesOfGlory.TestSupport` namespace. Move a double there once both projects need it, rather
than copying it; keep doubles only one test class needs as private nested classes in that test.
