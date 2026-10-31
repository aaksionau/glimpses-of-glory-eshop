---
status: accepted
---

# External integrations live in a separate GlimpsesOfGlory.Infrastructure project

External integrations were placed inconsistently: the Stripe client (`StripePaymentGateway`) and local-disk photo storage (`ProductPhotoStorage`) sat in `Core/Services/` next to domain logic, so Core carried the Stripe.net package, while the Resend email sender lived in Web. We decided (issue #152) that adapters to the outside world go in a separate `GlimpsesOfGlory.Infrastructure` project that references **only Abstractions** and implements its interfaces; Core references neither Infrastructure nor any integration SDK, and only `Program.cs` in Web wires Infrastructure types into DI. Project references make Core→Infrastructure impossible to compile, and `ProjectBoundaryTests` (NetArchTest) enforces the rest: Core must not depend on Infrastructure or Stripe, Infrastructure must not depend on Core, and no Web type but `Program` may depend on Infrastructure.

## Considered Options

- **`Core/Infrastructure/` folder** — cheapest move, but the separation is only a naming convention: nothing stops a domain service from newing up the Stripe client, and Core keeps the Stripe.net dependency.
- **Status quo, documented** — writes the rule down but leaves the inconsistency and the SDK dependency in Core.
- **Separate project (chosen)** — the boundary is enforced by the compiler and by architecture tests, and Core becomes integration-free.

## Consequences

- **The database is not an "integration" here.** Core owns EF Core, Npgsql, `AppDbContext` and the migrations, and its services query `AppDbContext` directly (there is deliberately no repository layer). `EfInventoryStore` therefore **stays in `Core/Services/`**: moving it would force Infrastructure to reference Core, exposing entities and `AppDbContext` to every adapter and losing the "Infrastructure depends on Abstractions only" rule, for a class that is just atomic `ExecuteUpdate` stock queries against Core's own model. `IInventoryStore` remains the test seam.
- **Resend email stays in `Web/Email/`** as the documented exception. `ResendEmailSender` renders the Razor templates in `Web/Emails/` through `RazorViewToStringRenderer`, which needs the MVC view engine; moving it would mean either an ASP.NET Core Razor dependency in Infrastructure or splitting the HTTP call off behind a new interface used by one class. Neither pays for itself today; if a second email transport or a non-Razor template engine appears, revisit.
- A Core service that needs an integration depends on an interface in `Abstractions/Services/` (e.g. `ProductPhotoStorage` gained `IProductPhotoStorage` so `AdminProductService` no longer names the concrete class).
- Configuration types that Web also reads (e.g. `StripeOptions.PublishableKey` on the payment page) stay in `Web/Configuration/`; `Program.cs` passes the values into the Infrastructure constructors. Options read only by an adapter (`ProductPhotoStorageOptions`) live in `Infrastructure/Options/`.
