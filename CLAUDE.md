# CLAUDE.md

This file guides Claude Code when working in this repository.

## Project overview

**OnlineShopping-MVC** is a single-store online shop for **small businesses** (few to moderate customers), built with
**ASP.NET Core MVC on .NET 10 (LTS)**, **EF Core 10 + SQL Server**, **ASP.NET Core Identity** (default Identity UI)
and server-side Blazor components embeddable in MVC views.

**The phased plan lives in [docs/ROADMAP.md](docs/ROADMAP.md)** (Persian). Read it before starting work, work on one
phase at a time, and update the phase status there when a phase is done. Phase 0 (foundation fixes) is complete;
only the template `HomeController` exists so far.

Target features (see roadmap): multilingual (fa, en, hy, de, ar — easy to add more, RTL for fa/ar), light/dark theme,
storefront UX inspired by large Iranian marketplaces, product images + videos, reviews, best sellers, special offers,
filtering/sorting/search, inventory with stocktaking, double-entry accounting, external logins (Google, Facebook,
Microsoft), payment gateways (Stripe, PayPal, Paddle, ZarinPal, Sadad, Behpardakht), user/admin/sysadmin docs.

## Non-negotiable rules

1. **Every new class gets a matching unit test** in `tests/`. Don't finish a task with failing or missing tests.
2. **SOLID / Clean Code**: thin controllers, business logic in services behind interfaces, no logic in views,
   small focused classes, constructor injection, `TimeProvider` instead of `DateTime.UtcNow` in logic.
3. **Ownership checks (IDOR)**: any id from the URL, query, or a (hidden) form field must be checked against the
   current user in the query itself (`x.Id == id && x.CustomerId == userId`), returning 404 for others.
   Staff roles may bypass. Take the user id from claims, never from input. (This bug was fixed in the sister
   project BulkyWeb in both the cart and orders.)
4. **No secrets in Git**: use `dotnet user-secrets` (UserSecretsId is set) or environment variables. Keys for
   Stripe/PayPal/etc. go under configuration sections bound with the Options pattern.
5. **Prices and totals are computed on the server**, never taken from the request. Payment success is confirmed only
   by server-side verify/webhook with signature validation.
6. **Copyright**: the UI may be *inspired by* Digikala-style layouts, but never copy their logo, name, brand colors,
   icons, images, texts or CSS. Use our own palette, Bootstrap Icons and OFL fonts (e.g. Vazirmatn).
7. **Identity generics always use `AppUser`**, never `IdentityUser` — including in scaffolded Identity pages
   (scaffolding defaults to `IdentityUser`; this broke login/logout in BulkyWeb).
8. Keep packages free of known vulnerabilities: `dotnet list package --vulnerable --include-transitive`; pin a patched
   version directly when a transitive dependency is vulnerable (see the `System.Security.Cryptography.Pkcs` pin).

## Repository layout

```
OnlineShopping-MVC.sln
dotnet-tools.json                   # local tool manifest (dotnet-ef 10.0.14)
docs/ROADMAP.md                     # phases, decisions, open questions
OnlineShopping-MVC/                 # web project (RootNamespace: OnlineShopping_MVC)
  Program.cs                        # DI, pipeline, routes, startup migrate + seed
  Data/
    AppDbContext.cs                 # IdentityDbContext<AppUser>; Fluent API config, soft-delete filters
    Interceptors/AuditableEntityInterceptor.cs   # CreatedUtc/ModifiedUtc + soft delete
    DataSeeder.cs, SeedOptions.cs   # migrations (optional), roles, first admin from config
  Migrations/                       # EF Core migrations (SQL Server)
  Models/Domain/                    # EF entities (all inherit BaseEntity)
  Models/Identity/                  # AppUser, AppRoles
  Controllers/, Views/, wwwroot/    # wwwroot/lib is vendored — don't edit
tests/OnlineShopping-MVC.Tests/     # xUnit; SQLite in-memory for EF tests
```

Phase 1 of the roadmap splits this into Domain / Application / Infrastructure / Web projects.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project OnlineShopping-MVC                          # http://localhost:5087
dotnet tool restore                                              # once, installs local dotnet-ef
dotnet tool run dotnet-ef migrations add <Name> --project OnlineShopping-MVC
dotnet tool run dotnet-ef database update --project OnlineShopping-MVC
dotnet tool run dotnet-ef migrations has-pending-model-changes --project OnlineShopping-MVC
```

Run a single test: `dotnet test --filter "FullyQualifiedName~AuditableEntityInterceptorTests"`.

After any model change: add a migration, then run `has-pending-model-changes` (must report no changes).

## Configuration

- `ConnectionStrings:Default` — `appsettings.json` (SQL Server `localhost`), overridden in
  `appsettings.Development.json` with LocalDB `OnlineShopDb_Dev`.
- `Database:MigrateOnStartup` — `true` in Development; applies pending migrations at startup.
- `Database:AdminEmail` / `Database:AdminPassword` — first admin account; set with user-secrets:
  `dotnet user-secrets set "Database:AdminPassword" "<password>" --project OnlineShopping-MVC`.
  Without them the seeder logs a warning and creates only roles.
- Identity options in `Program.cs` (digit required, min length 6 — to be hardened in roadmap phase 2).

## Data model & persistence conventions

- **BaseEntity**: `Guid Id`, `CreatedUtc`, `ModifiedUtc?`, `IsDeleted`. Never set these by hand —
  `AuditableEntityInterceptor` stamps them, and `Remove()` becomes a soft delete. A global query filter hides deleted
  rows (`IgnoreQueryFilters()` to see them). Only *tracked* cascade dependents are soft-deleted along with the parent.
- New `BaseEntity` types get the soft-delete filter automatically (`ApplySoftDeleteQueryFilters`).
- Unique indexes use `.HasFilter(NotDeletedFilter)` so soft-deleted rows don't block reuse (Slug, SKU, cart).
- All `decimal` columns are `decimal(18,2)` by convention (`ConfigureConventions`).
- User references (`Cart.CustomerId`, `Order.CustomerId`, `Product.SellerId`) are FKs to `AspNetUsers` **without
  navigation properties**, to keep the domain free of Identity. Orders/products use `Restrict`; carts cascade.
- `OrderItem.ProductName` / `UnitPrice` are snapshots at order time — never read current product data for old orders.
- `OrderStatus` is stored as a string; the `CK_Orders_Status` check constraint is generated from the enum
  (`BuildOrderStatusCheckSql`), so adding a value only needs a new migration.
- Configure everything with Fluent API in `AppDbContext`, one private `Configure*` method per area.
- Catalog: Category → SubCategory (`Groups`) → ProductGroup → Product → ProductImage (cascade).

## Code style

- C# with `Nullable` and `ImplicitUsings`; file-scoped namespaces; primary constructors for DI.
- Namespace root `OnlineShopping_MVC` (underscore); folder/project name `OnlineShopping-MVC` (hyphen).
- `required` for mandatory strings, `= default!` for navigations, collections initialized with `new List<T>()`.
- Use `AppRoles` constants in `[Authorize(Roles = ...)]`, never string literals.
- `internal` members used by tests are visible via `InternalsVisibleTo` (`OnlineShopping-MVC.Tests`).

## Testing

- xUnit + `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`).
- EF tests use `TestSupport/SqliteTestDatabase` (relational SQLite in-memory built from the real model) — not the EF
  InMemory provider, which ignores FKs, unique indexes and check constraints.
- Test names describe behavior: `Remove_soft_deletes_the_row_instead_of_deleting_it`.

## Commits

Messages start with a Solar Hijri date, e.g. `1405-08-17 Fix soft delete and audit fields`.
