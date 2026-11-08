# CLAUDE.md

This file guides Claude Code when working in this repository.

## Project overview

**OnlineShopping-MVC** is a single-store online shop for **small businesses** (few to moderate customers). It must be
**cross-platform** (Linux and Windows servers), a **mobile-first PWA**, fast, and secure.

Current state: one ASP.NET Core 10 project (MVC template + Identity UI) on **PostgreSQL 18**, plus a unit-test
project. **Target architecture (roadmap phase 1): a monorepo with a Next.js (React + TypeScript) frontend in
`apps/web` and an ASP.NET Core 10 Web API backend split into Domain / Application / Infrastructure / Api projects.**
The Razor views are temporary and will be removed when the Next.js app takes over.

**The phased plan and all recorded decisions live in [docs/ROADMAP.md](docs/ROADMAP.md)** (Persian). Read it before
starting work, work on one phase at a time, and mark the phase status there when a phase is done. Phase 0 is complete.

Recorded decisions (details in the roadmap):
- **Database:** code-first EF Core, **separate migrations per provider**. PostgreSQL first (`snake_case`), then
  SQL Server and MySQL. Keep provider-specific SQL inside that provider's migrations/config.
- **Roles:** works both as a **one-person shop** (owner has only `Admin`, which covers everything) and with
  **divided staff roles** (`Manager`, `Warehouse`, `Accountant`). There is no Seller / multi-vendor role.
- **Single-tenant:** one container + one database per shop; no `tenant_id`. All shops run the same versioned image.
- **Scope:** online B2C sales of physical goods only — no POS, digital products or B2B (backlog).
- **Currencies & gateways** are configured per deployment by the system admin (Europe: EUR/USD; Armenia: AMD/EUR/USD;
  Iran: IRR only, with ZarinPal/Sadad/Behpardakht; Stripe/PayPal elsewhere). Paddle is out (digital goods only).
  Prices are entered in a base currency and converted with admin-set rates + per-currency rounding (manual override
  allowed); an order stores the currency and rate it was paid with. Money always carries a currency.
- **Iran deployment:** hosted in Iran, SMS OTP + email login, external logins off. **No runtime dependency on external
  services in any deployment** — self-host fonts, icons, scripts and images (no CDNs, no Google Fonts).
- **SMS is optional**: everything must work with SMS disabled (default `NullSmsSender`).
- **Tax:** one simple rate per shop, prices incl./excl. tax, PDF invoices with sequential numbers. No e-invoicing
  integrations (Moadian, e-HDM, EU VAT OSS) in v1.
- **Branding:** admin-editable logo/colors/fonts/banners plus several ready-made layout templates (no rebuild needed).
- Languages fa, en, hy, de, ar (RTL for fa/ar); adding a language must be cheap. Product content is translated
  **manually only** (no external translation service); untranslated fields fall back to the default language.
- Light and dark theme. v1 ships to all markets at once.

## Non-negotiable rules

1. **Every new class gets a matching unit test** (.NET: `tests/`, xUnit; frontend: Vitest). Don't finish a task with
   failing or missing tests.
2. **SOLID / Clean Code**: thin endpoints/controllers, business logic in services behind interfaces, no logic in
   views/components beyond presentation, small focused classes, constructor injection, `TimeProvider` instead of
   `DateTime.UtcNow` in logic.
3. **Ownership checks (IDOR)**: any id from the URL, query, or request body must be checked against the current user
   in the query itself (`x.Id == id && x.CustomerId == userId`), returning 404 for others. Staff roles may bypass.
   Take the user id from claims, never from input. (This bug was fixed in the sister project BulkyWeb in both the
   cart and orders.)
4. **No secrets in Git**: use `dotnet user-secrets` (UserSecretsId is set), `.env.local` (git-ignored) or environment
   variables. Gateway/SMS keys go under configuration sections bound with the Options pattern.
5. **Prices and totals are computed on the server**, never taken from the request. Payment success is confirmed only
   by server-side verify/webhook with signature validation.
6. **Copyright**: the UI may be *inspired by* Digikala-style layouts, but never copy their logo, name, brand colors,
   icons, images, texts or CSS. Use our own palette, open-licensed icons and OFL fonts (e.g. Vazirmatn).
7. **Identity generics always use `AppUser`**, never `IdentityUser` — including in scaffolded Identity pages
   (scaffolding defaults to `IdentityUser`; this broke login/logout in BulkyWeb).
8. Keep packages free of known vulnerabilities: `dotnet list package --vulnerable --include-transitive` and
   `npm audit`; pin a patched version directly when a transitive dependency is vulnerable.

## Repository layout

```
OnlineShopping-MVC.sln
dotnet-tools.json                   # local tool manifest (dotnet-ef 10.0.14)
docs/ROADMAP.md                     # phases + recorded decisions
OnlineShopping-MVC/                 # backend (RootNamespace: OnlineShopping_MVC) -> becomes src/OnlineShop.Api
  Program.cs                        # DI, pipeline, routes, startup migrate + seed
  Data/
    AppDbContext.cs                 # IdentityDbContext<AppUser>; Fluent API config, soft-delete filters
    Interceptors/AuditableEntityInterceptor.cs   # CreatedUtc/ModifiedUtc + soft delete
    DataSeeder.cs, SeedOptions.cs   # migrations (optional), roles, first admin from config
  Migrations/PostgreSql/            # EF Core migrations for PostgreSQL (one folder/assembly per provider)
  Models/Domain/                    # EF entities (all inherit BaseEntity)
  Models/Identity/                  # AppUser, AppRoles
  Controllers/, Views/, wwwroot/    # temporary MVC UI; wwwroot/lib is vendored, don't edit
tests/OnlineShopping-MVC.Tests/     # xUnit; SQLite in-memory for EF tests
```

## Commands

```bash
dotnet build
dotnet test
dotnet run --project OnlineShopping-MVC                          # http://localhost:5087
dotnet tool restore                                              # once, installs local dotnet-ef
dotnet tool run dotnet-ef migrations add <Name> --project OnlineShopping-MVC --output-dir Migrations/PostgreSql
dotnet tool run dotnet-ef database update --project OnlineShopping-MVC
dotnet tool run dotnet-ef migrations has-pending-model-changes --project OnlineShopping-MVC
```

Run a single test: `dotnet test --filter "FullyQualifiedName~AuditableEntityInterceptorTests"`.

After any model change: add a migration, then run `has-pending-model-changes` (must report no changes).

Local PostgreSQL 18: Windows service `postgresql-x64-18`, port 5432, `psql` at
`C:\Program Files\PostgreSQL\18\bin\psql.exe`. The dev database `onlineshop_dev` is owned by the least-privilege
login `onlineshop`; the app never connects as the `postgres` superuser.

## Configuration

- `ConnectionStrings:Default` — `appsettings.json` holds a password-less template; the real dev connection string
  (user `onlineshop`) is in **user-secrets**, as is `Postgres:SuperuserPassword` (admin tasks only).
  Never write either password into a tracked file or into chat output.
- `Database:MigrateOnStartup` — `true` in Development; applies pending migrations at startup.
- `Database:AdminEmail` / `Database:AdminPassword` — first admin account, set with user-secrets:
  `dotnet user-secrets set "Database:AdminPassword" "<password>" --project OnlineShopping-MVC`.
  Without them the seeder logs a warning and creates only roles.
- Identity options in `Program.cs` (digit required, min length 6 — to be hardened in roadmap phase 2).

## Data model & persistence conventions

- **Naming:** `UseSnakeCaseNamingConvention()` — tables/columns are `snake_case`; Identity tables are renamed to
  `users`, `roles`, `user_roles`, ... in `ConfigureIdentityTables`. Raw SQL fragments (index filters, check
  constraints) use the snake_case names, e.g. `is_deleted = false`.
- **BaseEntity**: `Guid Id` (version 7, time-ordered), `CreatedUtc`, `ModifiedUtc?`, `IsDeleted`. Never set these by
  hand — `AuditableEntityInterceptor` stamps them, and `Remove()` becomes a soft delete. A global query filter hides
  deleted rows (`IgnoreQueryFilters()` to see them). Only *tracked* cascade dependents are soft-deleted with the parent.
- All `DateTime` values are UTC (`timestamptz` in PostgreSQL; Npgsql rejects non-UTC kinds).
- New `BaseEntity` types get the soft-delete filter automatically (`ApplySoftDeleteQueryFilters`).
- Unique indexes use `.HasFilter(NotDeletedFilter)` so soft-deleted rows don't block reuse (Slug, SKU, cart).
- All `decimal` columns are `numeric(18,2)` by convention (`ConfigureConventions`).
- User references (`Cart.CustomerId`, `Order.CustomerId`) are FKs to `users` **without navigation properties**, to
  keep the domain free of Identity. Orders use `Restrict`; carts cascade.
- `OrderItem.ProductName` / `UnitPrice` are snapshots at order time — never read current product data for old orders.
- `OrderStatus` is stored as a string; the `CK_Orders_Status` check constraint is generated from the enum
  (`BuildOrderStatusCheckSql`), so adding a value only needs a new migration.
- Configure everything with Fluent API in `AppDbContext`, one private `Configure*` method per area.
- Catalog: Category -> SubCategory (`Groups`) -> ProductGroup -> Product -> ProductImage (cascade).

## Code style

- C# with `Nullable` and `ImplicitUsings`; file-scoped namespaces; primary constructors for DI.
- Namespace root `OnlineShopping_MVC` (underscore); folder/project name `OnlineShopping-MVC` (hyphen).
- `required` for mandatory strings, `= default!` for navigations, collections initialized with `new List<T>()`.
- Use `AppRoles` constants (`AppRoles.All`, `AppRoles.Staff`) in authorization, never string literals.
- `internal` members used by tests are visible via `InternalsVisibleTo` (`OnlineShopping-MVC.Tests`).
- Cross-platform only: no Windows-specific paths, registry, Windows auth or `Trusted_Connection`.

## Testing

- xUnit + `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`).
- EF tests use `TestSupport/SqliteTestDatabase` (relational SQLite in-memory built from the real model, with the same
  snake_case convention) — not the EF InMemory provider, which ignores FKs, unique indexes and check constraints.
  Keep raw SQL fragments valid in both PostgreSQL and SQLite. Phase 1 adds integration tests on real PostgreSQL.
- Test names describe behavior: `Remove_soft_deletes_the_row_instead_of_deleting_it`.

## Commits

Messages start with a Solar Hijri date, e.g. `1405-08-17 Fix soft delete and audit fields`.
