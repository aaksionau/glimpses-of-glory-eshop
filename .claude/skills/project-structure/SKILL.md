---
name: project-structure
description: Explains this repo's solution layout — the Abstractions/Core/Web/Tests projects, the feature-first folder convention inside Core, and the Web-to-Core boundary rule. Use when deciding where a new file belongs, adding a new feature slice, wiring up a new PageModel or service, or exploring how this codebase is organized.
---

# Project structure

Four projects (`GlimpsesOfGloryEshop.slnx`): `GlimpsesOfGlory.Abstractions`, `GlimpsesOfGlory.Core`, `GlimpsesOfGlory.Web`, `tests/GlimpsesOfGlory.UnitTests`.

```
src/
  GlimpsesOfGlory.Abstractions/    (no project references)
    Products/   IProductCatalogService, ProductSummary, ProductDetail
    Cart/       ICartService, ICartStore, Cart, CartLine, CartSummary

  GlimpsesOfGlory.Core/            (references Abstractions only)
    Products/   Entities/ Services/ Persistence/ Seeding/
    Cart/       Services/
    Shipping/   Services/ ValueObjects/
    AppDbContext.cs, Migrations/

  GlimpsesOfGlory.Web/             (references both; see boundary rule below)
    Pages/, Cart/SessionCartStore.cs, Configuration/, Security/, Program.cs
```

Each feature folder is organized by **kind** underneath (`Entities/`, `Services/`, `Persistence/`, etc.), not by architectural layer — the top-level split is by feature, not Domain/Application/Infrastructure.

## The boundary rule

`Web` may reference `Core` types (`AppDbContext`, concrete service classes, `ShippingCalculator`, migrations) **only from `Program.cs`**, for DI wiring and EF tooling. Every `PageModel` or component injects the interfaces from `Abstractions` (`IProductCatalogService`, `ICartService`) — never a concrete `Core` type, never `AppDbContext` directly.

This isn't compiler-enforced — .NET project references don't support per-file restriction, so nothing blocks a `PageModel` from injecting `AppDbContext` if someone writes it that way. Treat it as a review-time invariant: if you find a `PageModel` touching `Core.*` or `AppDbContext` outside `Program.cs`, that's a bug to fix, not a pattern to extend.

`Abstractions` holds only what actually needs to cross that boundary: per-feature service interfaces, and the DTOs/entities both sides need to see (e.g. `Cart`/`CartLine` are plain data with light behavior, shared because `Web`'s `SessionCartStore` serializes them and `Core`'s `CartService` mutates them). EF entities (`Product`, `ProductPhoto`) live in `Core` only — only their DTO projections (`ProductSummary`, `ProductDetail`) cross into `Abstractions`.

## Why there's no repository layer

`IProductRepository` was deliberately removed. `ProductCatalogService` (in `Core`) queries `AppDbContext` directly — EF's `DbContext` already is a unit-of-work/query gateway, so a narrow CRUD-per-entity interface around it was indirection with no real substitutability payoff at this app's size. Don't reintroduce per-entity repository interfaces. If a new interface is genuinely needed across the boundary, add one coarse interface per feature/use-case to `Abstractions` (a handful of methods), not one per entity.

## EF configuration

Entity mapping lives per-feature as `IEntityTypeConfiguration<T>` classes under each feature's `Persistence/` folder (e.g. `Products/Persistence/ProductConfiguration.cs`), picked up via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `AppDbContext.OnModelCreating`. Don't add mapping code back into one large `OnModelCreating` method.

## Naming gotcha: CS0118

When a feature folder's name matches an entity's simple type name (`Cart/Cart.cs`), spelling that type out inside a file whose own namespace also has a `Cart` segment triggers `CS0118` ("'Cart' is a namespace but is used like a type"). Work around it with a type alias — see `GlimpsesOfGlory.Web/Cart/SessionCartStore.cs`'s `using CartModel = GlimpsesOfGlory.Abstractions.Cart.Cart;` — rather than renaming the feature folder.

## Adding a new feature

1. `Core/<Feature>/` with whatever kind-subfolders it needs (`Entities/`, `Services/`, `Persistence/`, ...).
2. `Abstractions/<Feature>/` only for the pieces that must cross into `Web` (a service interface, DTOs) — entities `Web` never touches stay in `Core` alone.
3. Wire the new service into DI in `Program.cs`.
