# OnlineShopping

A fast, secure, multilingual online shop for small businesses: **.NET 10 Web API + PostgreSQL** backend and a
**Next.js PWA** storefront, in one repository. Runs on Linux and Windows (Docker).

- Plan and decisions: [docs/ROADMAP.md](docs/ROADMAP.md) (Persian) · SEO/marketing design: [docs/SEO-MARKETING.md](docs/SEO-MARKETING.md)
- Deploying a shop: [deploy/README.md](deploy/README.md)
- Working on the code (also for AI assistants): [CLAUDE.md](CLAUDE.md)

```
src/       .NET: Domain, Application, Infrastructure, Infrastructure.Migrations.PostgreSql, Api
apps/web/  Next.js storefront (PWA)
tests/     .NET unit and integration tests
deploy/    Dockerfiles, docker-compose, nginx example
docs/      roadmap and design documents
```

## Development quick start

Requirements: .NET SDK 10, Node.js 26 LTS, PostgreSQL 18.

```bash
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=onlineshop_dev;Username=...;Password=..." --project src/OnlineShopping.Api
dotnet run --project src/OnlineShopping.Api      # http://localhost:5087, migrates the dev database
cd apps/web && npm install && npm run dev        # http://localhost:3000
```

Tests: `dotnet test` (integration tests need `IntegrationTests:AdminConnection`, see CLAUDE.md) and `npm test` in `apps/web`.
