---
name: project-structure
description: Explains this repo's solution layout — the Abstractions/Core/Infrastructure/Web/Tests projects, the type-first folder convention inside Core, Infrastructure and Abstractions, the Web-to-Core boundary rule, and where external integrations go. Use when deciding where a new file belongs, adding a new feature or integration, wiring up a new PageModel or service, or exploring how this codebase is organized.
---

# Project structure

Six projects (`GlimpsesOfGloryEshop.slnx`): `GlimpsesOfGlory.Abstractions`, `GlimpsesOfGlory.Core`, `GlimpsesOfGlory.Infrastructure`, `GlimpsesOfGlory.Web`, `tests/GlimpsesOfGlory.UnitTests` (no database, no Docker), `tests/GlimpsesOfGlory.IntegrationTests` (every test that touches a database or the HTTP pipeline, against real Postgres via Testcontainers; needs Docker). Each test project's `README.md` says what belongs in it. Test doubles and helpers used by both (`FakeCartStore`, `FakeInventoryStore`, `InMemorySession`, `TestRazorRenderer`, …) live once in `tests/TestSupport/` (namespace `GlimpsesOfGlory.TestSupport`), which both projects compile in — when a double starts being used by the second project, move it there rather than copying it; doubles only one project uses stay in that project.

```
src/
  GlimpsesOfGlory.Abstractions/    (no project references)
    Services/     ICartService, ICartStore, IProductCatalogService, IOrderService,
                  IPaymentGateway, IProductPhotoStorage, IEmailSender, ...
    Dtos/         Cart, CartLine, CartSummary, ProductSummary, ProductDetail, AdminOrderDetail, ...
    Enums/        OrderStatus
    Exceptions/   PaymentIntentUnavailableException, PaymentSignatureVerificationException
    Constants/    PreorderPolicy

  GlimpsesOfGlory.Core/            (references Abstractions only)
    Entities/             Product, ProductPhoto, Order, OrderLine, PendingCheckout, ShippingTierSetting, ...
    Services/             ProductCatalogService, CartService, OrderService, ShippingCalculator,
                          EfInventoryStore, ...
    EntityConfigurations/ ProductConfiguration, OrderConfiguration, ShippingTierSettingConfiguration, ...
    Extensions/           CheckoutEntityConfigurationExtensions, DbUpdateExceptionExtensions
    ValueObjects/         ShippingAddress, ShippingTier
    AppDbContext.cs, Migrations/

  GlimpsesOfGlory.Infrastructure/  (references Abstractions only — never Core)
    Services/     StripePaymentGateway (IPaymentGateway, Stripe.net), ProductPhotoStorage (IProductPhotoStorage, local disk)
    Options/      ProductPhotoStorageOptions

  GlimpsesOfGlory.Web/             (references all three; see boundary rule below)
    Pages/        Razor Pages (routing folders)
    Emails/       Razor email templates (OrderConfirmation.cshtml, ...)
    Email/        ResendEmailSender (IEmailSender over Resend's HTTP API), RazorViewToStringRenderer
    TagHelpers/   FieldValidationAriaTagHelper, ValidationMessageIdTagHelper, FieldValidationMessages
    Formatting/   display text: *Labels, OrderBadges, OrderTimeline, RelativeTime, SeoText,
                  PreorderNoteFormatter, CheckoutMessages
    Helpers/      what fits no more specific kind: session stores, CheckoutStepGuard,
                  AdminOrderListState, OrderCsvExport, nav sections, ...
    Endpoints/    minimal-API endpoints mapped from Program.cs: SeoEndpoints (robots.txt,
                  sitemap.xml), StripeWebhookEndpoint (/webhooks/stripe)
    Dtos/ (CheckoutAddress.cs), Configuration/, Security/, Program.cs
```

`Abstractions`, `Core` and `Infrastructure` are organized **type-first and flat**: the top-level split is by kind (`Services/`, `Dtos/`, `Entities/`, `EntityConfigurations/`, ...), and files sit directly in that folder — no per-feature subfolder underneath. A file's feature is conveyed by its name and class name (`ProductCatalogService.cs`, `OrderConfiguration.cs`), not by its path. This means the namespace for every file in a given kind-folder is the same (`GlimpsesOfGlory.Core.Services`, `GlimpsesOfGlory.Abstractions.Dtos`, etc.) regardless of which feature it belongs to.

`Web` does **not** use this layout: it has no `Cart/` or `Checkout/` folders of its own (`Pages/Cart`, `Pages/Checkout` are just Razor Pages routing folders, not feature or kind folders). Web-only support types are still grouped by kind: tag helpers go in `TagHelpers/`, email sending/rendering in `Email/`, display-label and message formatters in `Formatting/`, plain data-transfer types in `Dtos/`, and session stores and other helpers that fit no more specific kind in `Helpers/`. Namespaces follow the folder (`GlimpsesOfGlory.Web.Formatting`, ...); `Pages/_ViewImports.cshtml` imports `Formatting` and `Helpers` for all pages. Keep this flat — don't reintroduce `Web/<Feature>/` folders.

## The boundary rule

`Web` may reference `Core` and `Infrastructure` types (`AppDbContext`, concrete service classes, `ShippingCalculator`, `StripePaymentGateway`, migrations) **only from `Program.cs`**, for DI wiring and EF tooling. Every `PageModel` or component injects the interfaces from `Abstractions` (`IProductCatalogService`, `ICartService`) — never a concrete `Core` type, never `AppDbContext` directly.

This isn't compiler-enforced — .NET project references don't support per-file restriction — but it **is test-enforced**: `tests/GlimpsesOfGlory.UnitTests/ProjectBoundaryTests.cs` uses NetArchTest to scan the IL of every type in the `GlimpsesOfGlory.Web` assembly (including Razor-compiled pages/views) and fails if any type other than `Program` depends on the `GlimpsesOfGlory.Core` namespace — in signatures *or* method bodies (so `new AppDbContext(...)`, `GetRequiredService<AppDbContext>()` or `@inject AppDbContext` are caught too). The failure message lists each offending Web type and the Core types it references. If that test fails, fix the Web type to use an `Abstractions` interface — don't loosen the test. The same test applies the identical rule to the `GlimpsesOfGlory.Infrastructure` namespace.

`Abstractions` holds only what actually needs to cross that boundary: service interfaces (`Services/`), and the DTOs/records both sides need to see (`Dtos/`) (e.g. `Cart`/`CartLine` are plain data with light behavior, shared because `Web`'s `SessionCartStore` serializes them and `Core`'s `CartService` mutates them). EF entities (`Product`, `ProductPhoto`) live in `Core/Entities/` only — only their DTO projections (`ProductSummary`, `ProductDetail`) cross into `Abstractions/Dtos/`.

## Where external integrations go

Decided in `docs/adr/0001-external-integrations-in-infrastructure-project.md`. An adapter to something outside the app — a payment provider, file/blob storage, a third-party HTTP API, an SDK package — goes in `GlimpsesOfGlory.Infrastructure/` (type-first: `Services/` for the adapter, `Options/` for settings only it reads), implementing an interface in `Abstractions/Services/`. Core services depend on that interface, never on the adapter or the SDK; the SDK `PackageReference` goes in `Infrastructure.csproj`, not Core's. Wire it in `Program.cs`. `ProjectBoundaryTests` fails if Core depends on Infrastructure or Stripe, if Infrastructure depends on Core, or if a Web type other than `Program` depends on Infrastructure.

Two deliberate exceptions:

- **The database is Core's, not an integration.** Anything that needs `AppDbContext` (e.g. `EfInventoryStore`) stays in `Core/Services/` — Infrastructure must not reference Core.
- **Email sending stays in `Web/Email/`.** `ResendEmailSender` renders the Razor templates in `Web/Emails/`, which needs the MVC view engine; don't add Razor to Infrastructure to move it.

## Why there's no repository layer

`IProductRepository` was deliberately removed. `ProductCatalogService` (in `Core/Services/`) queries `AppDbContext` directly — EF's `DbContext` already is a unit-of-work/query gateway, so a narrow CRUD-per-entity interface around it was indirection with no real substitutability payoff at this app's size. Don't reintroduce per-entity repository interfaces. If a new interface is genuinely needed across the boundary, add one coarse interface per feature/use-case to `Abstractions/Services/` (a handful of methods), not one per entity.

## EF configuration

Entity mapping lives in `Core/EntityConfigurations/` as `IEntityTypeConfiguration<T>` classes (e.g. `EntityConfigurations/ProductConfiguration.cs`), picked up via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `AppDbContext.OnModelCreating`. Shared configuration logic reused across entities (e.g. the checkout header/line setup common to `Order`/`PendingCheckout`) lives as extension methods in `Core/Extensions/`. Don't add mapping code back into one large `OnModelCreating` method.

## Naming gotcha: CS0118 (mostly avoided now)

`CS0118` ("'Cart' is a namespace but is used like a type") happens when a namespace segment matches a type's simple name and that type is referenced unqualified from within a scope where the namespace is also in play. The old feature-first layout hit this constantly (`Core.Cart.Services` vs the `Cart` type). The type-first layout mostly sidesteps it, since kind-folder namespaces (`Services`, `Dtos`, `Entities`, ...) don't collide with entity/DTO type names. If it does come up again, use a type alias (`using CartModel = GlimpsesOfGlory.Abstractions.Dtos.Cart;`) rather than restructuring folders around it.

## Adding a new feature

1. Add entities/services/configuration to the relevant `Core/<Kind>/` folders — `Entities/`, `Services/`, `EntityConfigurations/`, `ValueObjects/`, `Extensions/` as needed.
2. Add only what must cross into `Web` to the matching `Abstractions/<Kind>/` folder (`Services/` for the interface, `Dtos/` for DTOs, `Enums/`, `Exceptions/`, `Constants/` as needed) — entities `Web` never touches stay in `Core` alone.
3. If it talks to an external system, put the adapter in `Infrastructure/` behind the `Abstractions` interface (see above).
4. Wire the new service into DI in `Program.cs`.
