# OnlineShop web (Next.js)

Storefront and (later) admin UI. Next.js 16 App Router with Cache Components, TypeScript, Tailwind CSS 4, PWA.
It talks only to the .NET API (`src/OnlineShop.Api`), server-side; the browser reaches `/api/v1/*` through `src/proxy.ts`.

```bash
npm install
npm run dev          # http://localhost:3000 — needs the API on http://localhost:5087
npm test             # Vitest
npm run lint
npm run typecheck    # next typegen + tsc
npm run build        # works without a running API
npm run gen:api      # regenerate src/lib/api/schema.d.ts from openapi/OnlineShop.Api.json (written by the API build)
```

Runtime environment variables (nothing shop-specific is baked into the build):

| Variable | Purpose |
|---|---|
| `API_INTERNAL_URL` | API base URL on the private network (default `http://localhost:5087`) |
| `SITE_URL` | Public origin; `https://` enables HSTS and `upgrade-insecure-requests` |
| `SEO_ALLOW_INDEXING` | `true` only on the production shop; anything else = noindex everywhere |
| `REVALIDATE_SECRET` | Shared secret for `POST /api/revalidate` |

Store name, locales and default locale come from the API (`GET /api/v1/store`).
