# CLAUDE.md

This file guides Claude Code when working in this repository.

## Project overview

**OnlineShop** is a single-store online shop for **small businesses** (few to moderate customers). It must be
**cross-platform** (Linux and Windows servers), a **mobile-first PWA**, fast, and secure.

Monorepo: an **ASP.NET Core 10 Web API** on **PostgreSQL 18** (Clean Architecture: Domain / Application /
Infrastructure / Api) and a **Next.js 16** storefront in `apps/web`. Each shop is one Docker stack; the same images
serve every shop.

**The phased plan and all recorded decisions live in [docs/ROADMAP.md](docs/ROADMAP.md)** (Persian). Read it before
starting work, work on one phase at a time, and mark the phase status there when a phase is done. Phase 0 is done;
phase 1 is nearly done (see its "remaining" list).

Recorded decisions (details in the roadmap):
- **Database:** code-first EF Core, **separate migrations per provider** (one migrations project each). PostgreSQL
  first (`snake_case`), then SQL Server and MySQL. Keep provider-specific SQL inside that provider's migrations/config.
- **Roles:** works both as a **one-person shop** (owner has only `Admin`, which covers everything) and with
  **divided staff roles** (`Manager`, `Warehouse`, `Accountant`). There is no Seller / multi-vendor role.
- **Single-tenant:** one stack + one database per shop; no `tenant_id`. All shops run the same versioned images.
- **Scope:** online B2C sales of physical goods only — no POS, digital products or B2B (backlog).
- **Currencies & gateways** are configured per deployment by the system admin (Europe: EUR/USD; Armenia: AMD/EUR/USD;
  Iran: IRR only, with ZarinPal/Sadad/Behpardakht; Stripe/PayPal elsewhere). Paddle is out (digital goods only).
  Prices are entered in a base currency and converted with admin-set rates + per-currency rounding (manual override
  allowed); an order stores the currency and rate it was paid with. Money always carries a currency.
- **Iran deployment:** hosted in Iran, SMS OTP + email login, external logins off. **No runtime dependency on external
  services in any deployment** — self-host fonts, icons, scripts and images (no CDNs, no Google Fonts, no
  `next/font/google`). The only exception: marketing tags (GTM/GA4, Meta Pixel, Matomo) that the admin opts into,
  loaded only after cookie consent; the site must work fully without them.
- **SEO & marketing design: [docs/SEO-MARKETING.md](docs/SEO-MARKETING.md).** URLs `/{locale}/p/{number}/{slug}`
  (every locale prefixed, stable numeric product number, slug mismatch → 301), hreflang only for translated locales,
  JSON-LD, sitemaps, redirects, blog, product feeds (Google, Meta, Torob, Emalls), UTM attribution on orders.
- **SMS is optional**: everything must work with SMS disabled (default `NullSmsSender`).
- **Tax:** one simple rate per shop, prices incl./excl. tax, PDF invoices with sequential numbers. No e-invoicing
  integrations (Moadian, e-HDM, EU VAT OSS) in v1.
- **Branding:** admin-editable logo/colors/fonts/banners plus several ready-made layout templates (no rebuild needed).
- Languages fa, en, hy, de, ar (RTL for fa/ar); adding a language must be cheap. Product content is translated
  **manually only** (no external translation service); untranslated fields fall back to the default language.
- Light and dark theme. v1 ships to all markets at once.

## Non-negotiable rules

1. **Every new class gets a matching unit test** (.NET: `tests/`, xUnit; frontend: Vitest, `*.test.ts(x)` next to
   the file). Don't finish a task with failing or missing tests.
2. **SOLID / Clean Code**: thin endpoints, business logic in Application services behind interfaces, no logic in
   components beyond presentation, small focused classes, constructor injection, `TimeProvider` instead of
   `DateTime.UtcNow` in logic. Dependencies point inward: Api → Infrastructure → Application → Domain.
3. **Ownership checks (IDOR)**: any id from the URL, query, or request body must be checked against the current user
   in the query itself (`x.Id == id && x.CustomerId == userId`), returning 404 for others. Staff roles may bypass.
   Take the user id from claims, never from input. (This bug was fixed in the sister project BulkyWeb in both the
   cart and orders.)
4. **No secrets in Git**: `dotnet user-secrets` (API and integration tests have UserSecretsIds), `deploy/.env` and
   `.env*` files are git-ignored, environment variables in production. Gateway/SMS keys go under configuration
   sections bound with the Options pattern.
5. **Prices and totals are computed on the server**, never taken from the request. Payment success is confirmed only
   by server-side verify/webhook with signature validation.
6. **Copyright**: the UI may be *inspired by* Digikala-style layouts, but never copy their logo, name, brand colors,
   icons, images, texts or CSS. Use our own palette, open-licensed icons and OFL fonts (e.g. Vazirmatn).
7. **Identity generics always use `AppUser`**, never `IdentityUser` (this broke login/logout in BulkyWeb).
8. **Every public page is SEO-complete**: server-rendered content (works without client JS), title, description,
   canonical, hreflang, `lang`/`dir`, valid JSON-LD matching the visible price/stock, no layout shift. Filter/sort
   URLs canonicalize to the unfiltered page; never change a public URL without a 301. Non-production instances are
   always `noindex`. Use the acceptance checklist at the end of `docs/SEO-MARKETING.md`.
9. Keep packages free of known vulnerabilities: `dotnet list package --vulnerable --include-transitive` and
   `npm audit --omit=dev`; pin a patched version directly when a transitive dependency is vulnerable.
10. **Nothing shop-specific at build time** (one image for all shops): store settings live in the API
    (`Store` section); the web app reads them per request (`loadStore()`), and runtime env vars cover the rest.
    Never read shop settings in `next.config.ts` or at module scope.

## Repository layout

```
OnlineShop.sln                      # all .NET projects
Directory.Build.props               # net10.0, nullable, analyzers, warnings = errors
Directory.Packages.props            # central NuGet versions (no Version= in csproj files)
dotnet-tools.json                   # local tools (dotnet-ef 10.0.14)
src/
  OnlineShop.Domain/                # entities: Common/BaseEntity, Catalog/*, Sales/* — no dependencies
  OnlineShop.Application/           # use cases (Catalog/CategoryQueries), Common/Result + AppError,
                                    # IApplicationDbContext, StoreOptions, Authorization/AppRoles, AddApplication()
  OnlineShop.Infrastructure/        # Persistence/AppDbContext (+ interceptor, seeding, DatabaseOptions),
                                    # Identity/AppUser, AddInfrastructure() (EF, Identity, Data Protection, health)
  OnlineShop.Infrastructure.Migrations.PostgreSql/   # EF migrations for PostgreSQL
  OnlineShop.Api/                   # Program.cs, Endpoints/, Security/ (headers, reverse proxy), Common/ResultExtensions
tests/
  OnlineShop.UnitTests/             # xUnit; SQLite in-memory for EF; TestHost for middleware
  OnlineShop.IntegrationTests/      # WebApplicationFactory + real PostgreSQL (fresh database per test class)
apps/web/                           # Next.js 16 (see apps/web/README.md)
  src/app/[locale]/                 # pages (root layout lives here: lang/dir per locale)
  src/app/{robots,sitemap,manifest,icon}.ts(x), api/revalidate/route.ts
  src/proxy.ts                      # runtime: API forwarding, locale redirects, security headers, noindex
  src/lib/                          # api/ (typed client, `use cache` fetchers), store/, i18n/, seo/, security/, pwa/
  openapi/OnlineShop.Api.json       # written by the API build; source of src/lib/api/schema.d.ts
deploy/                             # api/web Dockerfiles, docker-compose.yml, .env.example, nginx.conf, README
docs/                               # ROADMAP.md, SEO-MARKETING.md
```

## Commands

```bash
# .NET
dotnet tool restore
dotnet build OnlineShop.sln                     # also regenerates apps/web/openapi/OnlineShop.Api.json
dotnet test OnlineShop.sln
dotnet test --filter "FullyQualifiedName~CategoryQueriesTests"
dotnet run --project src/OnlineShop.Api         # http://localhost:5087 (migrates the dev DB in Development)
dotnet tool run dotnet-ef migrations add <Name> --project src/OnlineShop.Infrastructure.Migrations.PostgreSql --startup-project src/OnlineShop.Api
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/OnlineShop.Infrastructure.Migrations.PostgreSql --startup-project src/OnlineShop.Api

# Next.js (in apps/web; Node is at C:\Program Files\nodejs — add it to PATH in Git Bash)
npm run dev | npm test | npm run lint | npm run typecheck | npm run build
npm run gen:api                                 # after any API contract change, then commit both files

# Deploy (Docker)
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

After a model change: add a migration, then `has-pending-model-changes` must report no changes.
After an API contract change: build the API, run `npm run gen:api`, commit `openapi/*.json` and `schema.d.ts`
(CI fails if either is stale).

## Local environment (this dev machine)

- PostgreSQL 18: service `postgresql-x64-18`, port 5432, `psql` at `C:\Program Files\PostgreSQL\18\bin\psql.exe`.
  - `onlineshop_dev` is owned by the least-privilege login `onlineshop`; the API never uses the `postgres` superuser.
  - `onlineshop_test` (CREATEDB) is used by integration tests, which create and drop `onlineshop_it_*` databases.
- Secrets (never print them, never write them to tracked files):
  - API user-secrets: `ConnectionStrings:Default`, `Postgres:SuperuserPassword` (admin tasks only),
    optionally `Database:AdminEmail` / `Database:AdminPassword`.
  - Integration-test user-secrets (`tests/OnlineShop.IntegrationTests`): `IntegrationTests:AdminConnection`
    (CI uses the `INTEGRATIONTESTS__ADMINCONNECTION` env var).
- No Docker on this machine yet; Docker images are built by CI.
- The built-in browser pane does not allow service workers; check PWA behaviour in a real browser.

## Backend conventions

- **Naming:** `UseSnakeCaseNamingConvention()`; Identity tables renamed to `users`, `roles`, … Raw SQL fragments
  (index filters, check constraints) use snake_case and must be valid in PostgreSQL and SQLite (`is_deleted = false`).
- **BaseEntity**: `Guid Id` (version 7), `CreatedUtc`, `ModifiedUtc?`, `IsDeleted` — stamped by
  `AuditableEntityInterceptor`; `Remove()` is a soft delete; a global query filter hides deleted rows
  (`IgnoreQueryFilters()` to see them). Unique indexes use `.HasFilter(NotDeletedFilter)`.
- All `DateTime`s are UTC (`timestamptz`); all `decimal`s are `numeric(18,2)`.
- User references (`Cart.CustomerId`, `Order.CustomerId`) are FKs to `users` without navigation properties
  (domain stays free of Identity). Orders `Restrict`, carts cascade.
- `OrderItem.ProductName`/`UnitPrice` are snapshots. `OrderStatus` is stored as text with a generated check constraint.
- **Use cases** depend on `IApplicationDbContext`; read queries use `AsNoTracking()` + projection to records.
- **Errors:** expected failures return `Result<T>` with an `AppError` (`ErrorKind` + stable `code` like
  `category.not_found`); endpoints map them with `ToHttpResult()` to RFC 9457 problem details carrying `code`.
- **Endpoints:** minimal APIs grouped under `/api/v1`, with `WithName` (becomes the OpenAPI operationId) and summary.
- **Options:** bind + `ValidateDataAnnotations` + `ValidateOnStart`. Configuration lists merge by index across sources,
  so list-like settings that operators override are comma-separated strings (see `StoreOptions.Locales`).
- Logging via `[LoggerMessage]` source-generated methods (analyzer CA1848).
- Code style: file-scoped namespaces, primary constructors, `sealed` by default, `required`/`= default!`.

## Accounts and authorization

- Accounts are identified by **email or phone** (at least one; each unique — `AppUserValidator` + filtered unique
  indexes `ux_users_normalized_email`, `ux_users_phone_number`). `UserName` is an internal random value. Phones are
  stored in E.164 via `IPhoneNumberNormalizer` (libphonenumber; accepts Persian/Arabic digits; `Store:PhoneRegion`).
- Session = Identity application cookie `os_session` (HttpOnly, SameSite=Lax, Secure unless `Auth:SecureCookies=false`),
  issued by the API and passed through the web proxy. The API answers 401/403, never redirects.
- CSRF: `OriginCheckMiddleware` rejects POST/PUT/PATCH/DELETE whose `Origin` is not `Store:PublicUrl`'s origin.
- Authorization: endpoints require **permissions**, not roles —
  `.RequireAuthorization(Permissions.Policy(Permissions.CatalogManage))`. Roles map to permissions in
  `RolePermissions` (fixed in code; Admin = all). Get the user id only via `User.GetUserId()`.
- Account use cases live in `Application/Accounts/AccountService` behind `IAccountIdentity` (Identity in
  Infrastructure). Request validation: FluentValidation validators + `.Validate<T>()` endpoint filter → 400 with
  `errors: { field: [codes] }`. Error codes are constants in `AccountErrors`; the web app translates them
  (`dictionaries/*.json` → `account.errors`), and a test fails if any code lacks a translation in any locale.
- No account enumeration: wrong identifier and wrong password give the same answer; forgot-password always 202.
- Sign-in/sign-up/recovery endpoints use the `auth` rate-limit policy (`RateLimiting:AuthPermitPerWindow`).
- Options that depend on configuration must be read through `IOptions` at runtime, never with
  `configuration.Get<T>()` while registering services — later sources (env vars, tests) would be ignored.
- Email: `IEmailSender` (`Email:Delivery` = `Smtp` in production, `Pickup` = `.eml` files under `App_Data/mail` in
  development). Texts in `EmailTemplates` for every locale; links always point at `Store:PublicUrl/{locale}/…`.
- SMS: `ISmsSender` with providers `Kavenegar` (Verify Lookup template), `SmsIr` (Verify template), `Twilio` (text),
  `Pickup` (development: `App_Data/sms/*.txt`), `None` (default). `ISmsCapability.IsEnabled` gates every SMS feature.
  Provider credentials are validated at startup. `System.Net.Http.HttpClient` logging stays at Warning: Kavenegar's API
  key is part of the request URL.
- Phone accounts: sign-in and sign-up are one flow (`/account/phone/send-code` → `/account/phone/sign-in`); a new number
  becomes a verified, password-less customer. Password sign-up is email only. `PhoneCodeService` owns the code rules
  (`PhoneCodes` options: length, lifetime, cooldown, attempts, hourly cap); only salted hashes are stored
  (`phone_verification_codes`). A lockout also blocks code sign-in. Rate limits return `ErrorKind.TooManyRequests` → 429.
- Tests that depend on time replace the DI `TimeProvider` with `FakeTimeProvider` instead of sleeping.
- Web: forms post JSON to `/api/v1/account/*` from the browser (`postJson`); server components read the user with
  `getCurrentUser()` (forwards only the session cookie, never cached). `proxy.ts` redirects signed-out visitors away
  from private `/{locale}/account/*` pages; `safeReturnPath()` prevents open redirects.

## Frontend conventions (Next.js 16 — read node_modules/next/dist/docs before using unfamiliar APIs)

- Cache Components + Partial Prefetching are on. Data fetchers use `"use cache"` + `cacheTag` + `cacheLife`;
  invalidate with `revalidateTag(tag, "max")` (two arguments in v16) via `POST /api/revalidate`.
- Anything store-specific or runtime-env-dependent is read inside `<Suspense>` after `await connection()`
  (`loadStore()` does this), so the build never needs the API.
- `middleware` is now `proxy` (`src/proxy.ts`). Nonce-based CSP is incompatible with prerendering, so the CSP is
  static with `'unsafe-inline'` scripts and everything else locked to `'self'`.
- Server-only modules import `"server-only"`; the API is called only from the server (`apiGet`), never from the browser.
- UI strings: `src/lib/i18n/dictionaries/<locale>.json`, all with the same keys as `fa.json` (enforced by a test).
- Tests: Vitest (`environment: node`; `// @vitest-environment jsdom` for components) + Testing Library.

## Commits

Messages start with a Solar Hijri date, e.g. `1405-08-17 Fix soft delete and audit fields`.
