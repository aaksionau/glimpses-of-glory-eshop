---
name: project-structure
description: Explains this repo's solution layout — the Abstractions/Core/Web/Tests projects, the type-first folder convention inside Core and Abstractions, and the Web-to-Core boundary rule. Use when deciding where a new file belongs, adding a new feature, wiring up a new PageModel or service, or exploring how this codebase is organized.
---

# Project structure

Four projects (`GlimpsesOfGloryEshop.slnx`): `GlimpsesOfGlory.Abstractions`, `GlimpsesOfGlory.Core`, `GlimpsesOfGlory.Web`, `tests/GlimpsesOfGlory.UnitTests`.

```
src/
  GlimpsesOfGlory.Abstractions/    (no project references)
    Services/     ICartService, ICartStore, IProductCatalogService, IOrderService, ...
    Dtos/         Cart, CartLine, CartSummary, ProductSummary, ProductDetail, AdminOrderDetail, ...
    Enums/        OrderStatus
    Exceptions/   PaymentIntentUnavailableException, PaymentSignatureVerificationException
    Constants/    PreorderPolicy

  GlimpsesOfGlory.Core/            (references Abstractions only)
    Entities/             Product, ProductPhoto, Order, OrderLine, PendingCheckout, ShippingTierSetting, ...
    Services/             ProductCatalogService, CartService, OrderService, ShippingCalculator, ...
    EntityConfigurations/ ProductConfiguration, OrderConfiguration, ShippingTierSettingConfiguration, ...
    Extensions/           CheckoutEntityConfigurationExtensions, DbUpdateExceptionExtensions
    ValueObjects/         ShippingAddress, ShippingTier
    Options/              ProductPhotoStorageOptions
    AppDbContext.cs, Migrations/

  GlimpsesOfGlory.Web/             (references both; see boundary rule below)
    Pages/, Helpers/ (SessionCartStore.cs, CheckoutSessionStore.cs), Dtos/ (CheckoutAddress.cs),
    Configuration/, Security/, Program.cs
```

Both `Abstractions` and `Core` are organized **type-first and flat**: the top-level split is by kind (`Services/`, `Dtos/`, `Entities/`, `EntityConfigurations/`, ...), and files sit directly in that folder — no per-feature subfolder underneath. A file's feature is conveyed by its name and class name (`ProductCatalogService.cs`, `OrderConfiguration.cs`), not by its path. This means the namespace for every file in a given kind-folder is the same (`GlimpsesOfGlory.Core.Services`, `GlimpsesOfGlory.Abstractions.Dtos`, etc.) regardless of which feature it belongs to.

`Web` does **not** use this layout: it has no `Cart/` or `Checkout/` folders of its own (`Pages/Cart`, `Pages/Checkout` are just Razor Pages routing folders, not feature or kind folders). Web-only support types are still grouped by kind: session stores and other helper classes go in `Helpers/`, plain data-transfer types go in `Dtos/`. Keep this flat — don't reintroduce `Web/<Feature>/` folders.

## The boundary rule

`Web` may reference `Core` types (`AppDbContext`, concrete service classes, `ShippingCalculator`, migrations) **only from `Program.cs`**, for DI wiring and EF tooling. Every `PageModel` or component injects the interfaces from `Abstractions` (`IProductCatalogService`, `ICartService`) — never a concrete `Core` type, never `AppDbContext` directly.

This isn't compiler-enforced — .NET project references don't support per-file restriction, so nothing blocks a `PageModel` from injecting `AppDbContext` if someone writes it that way. Treat it as a review-time invariant: if you find a `PageModel` touching `Core.*` or `AppDbContext` outside `Program.cs`, that's a bug to fix, not a pattern to extend.

`Abstractions` holds only what actually needs to cross that boundary: service interfaces (`Services/`), and the DTOs/records both sides need to see (`Dtos/`) (e.g. `Cart`/`CartLine` are plain data with light behavior, shared because `Web`'s `SessionCartStore` serializes them and `Core`'s `CartService` mutates them). EF entities (`Product`, `ProductPhoto`) live in `Core/Entities/` only — only their DTO projections (`ProductSummary`, `ProductDetail`) cross into `Abstractions/Dtos/`.

## Why there's no repository layer

`IProductRepository` was deliberately removed. `ProductCatalogService` (in `Core/Services/`) queries `AppDbContext` directly — EF's `DbContext` already is a unit-of-work/query gateway, so a narrow CRUD-per-entity interface around it was indirection with no real substitutability payoff at this app's size. Don't reintroduce per-entity repository interfaces. If a new interface is genuinely needed across the boundary, add one coarse interface per feature/use-case to `Abstractions/Services/` (a handful of methods), not one per entity.

## EF configuration

Entity mapping lives in `Core/EntityConfigurations/` as `IEntityTypeConfiguration<T>` classes (e.g. `EntityConfigurations/ProductConfiguration.cs`), picked up via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `AppDbContext.OnModelCreating`. Shared configuration logic reused across entities (e.g. the checkout header/line setup common to `Order`/`PendingCheckout`) lives as extension methods in `Core/Extensions/`. Don't add mapping code back into one large `OnModelCreating` method.

## Naming gotcha: CS0118 (mostly avoided now)

`CS0118` ("'Cart' is a namespace but is used like a type") happens when a namespace segment matches a type's simple name and that type is referenced unqualified from within a scope where the namespace is also in play. The old feature-first layout hit this constantly (`Core.Cart.Services` vs the `Cart` type). The type-first layout mostly sidesteps it, since kind-folder namespaces (`Services`, `Dtos`, `Entities`, ...) don't collide with entity/DTO type names. If it does come up again, use a type alias (`using CartModel = GlimpsesOfGlory.Abstractions.Dtos.Cart;`) rather than restructuring folders around it.

## Adding a new feature

1. Add entities/services/configuration to the relevant `Core/<Kind>/` folders — `Entities/`, `Services/`, `EntityConfigurations/`, `ValueObjects/`, `Options/`, `Extensions/` as needed.
2. Add only what must cross into `Web` to the matching `Abstractions/<Kind>/` folder (`Services/` for the interface, `Dtos/` for DTOs, `Enums/`, `Exceptions/`, `Constants/` as needed) — entities `Web` never touches stay in `Core` alone.
3. Wire the new service into DI in `Program.cs`.
