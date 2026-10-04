# Larder

Larder is a pantry-aware recipe app: it doesn't just store recipes, it knows what's actually in
your kitchen, tells you which recipes you can cook right now, and — for what you're missing —
looks up real nearby stores with real prices. It's built as a distributed system on purpose:
four independently deployable .NET services behind a GraphQL gateway, plus a React web-client,
designed to demonstrate production-grade backend engineering (service boundaries, resilience,
observability, real auth) rather than to be a CRUD demo with extra steps.

See [`docs/design.md`](docs/design.md) for the full architecture rationale, data model, and
decision history — including what was tried, what changed, and why.

## Architecture

```
web-client (React)  →  Gateway (GraphQL)  →  RecipeService, PantryService, SourcingService
```

| Service | Port | Owns | What it does |
|---|---|---|---|
| **Gateway** | `:5085` | — | GraphQL API (HotChocolate) that aggregates the three backend services over REST. Forwards the caller's bearer token downstream rather than validating it itself — each service enforces its own auth. |
| **RecipeService** | `:5081` | `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`, `users` (Postgres) | Recipes, steps, the shared ingredient catalog, and user identity (JIT-provisioned from Auth0 tokens on first request). Also ranks recipes by how many of a given ingredient list they match. |
| **PantryService** | `:5082` | `pantry_items` (Postgres) | Per-user pantry inventory (presence-only — has an ingredient or doesn't). Calls RecipeService for ingredient/recipe data rather than joining across databases, and publishes an `ingredient.missing` event when a recipe needs something you don't have. |
| **SourcingService** | `:5084` | — | "Where can I buy this" lookups: confirmed per-ingredient pricing via Kroger, general nearby stores via Google Places — run in parallel with per-provider timeouts, Redis-cached. |
| **web-client** | `:5173` | — | React + urql GraphQL client, Auth0 login. The intended way to use the app end-to-end. |

Every backend service validates Auth0 JWT bearer tokens itself and requires one by default —
health checks and the QStash webhook endpoint (which authenticates via its own HMAC signature
check instead) are the only anonymous routes. There's no dev-mode auth bypass; running this
locally requires a real Auth0 tenant. Recipe and ingredient writes additionally require an
`admin` role claim on the token, enforced both at the Gateway (for a clean GraphQL error) and
again at RecipeService (the real boundary).

## Tech stack

- **Backend**: C# / ASP.NET Core (.NET 8), four independently deployable services
- **API**: GraphQL (HotChocolate) at the Gateway, REST internally between Gateway and services
- **Data**: PostgreSQL (Neon) for relational data, Redis for sourcing-result caching
- **Events**: Upstash QStash (signed webhook delivery) for the `ingredient.missing` event
- **Auth**: Auth0 (JWT bearer tokens, role-based authorization, JIT user provisioning)
- **Frontend**: React 19 + Vite, urql, Tailwind v4 + shadcn/ui, Auth0 SPA SDK
- **External APIs**: Kroger (product/price search) and Google Places (store lookup)
- **Observability**: OpenTelemetry distributed tracing, Prometheus metrics, Grafana dashboards, Jaeger UI (local/dev)

## Technical highlights

**A relational-to-graph migration, built, verified, then removed.** Substitution logic started
as a plain Postgres table and was deliberately migrated to a Neo4j graph once the model needed
things a join table can't express cleanly (asymmetric relationships, context-dependent weights,
cheap transitive lookups). It was later removed entirely — no real substitution data was ever
populated, and the hosted Neo4j AuraDB instance wasn't being kept alive. The full rationale for
both the migration and the removal is in design.md's
[Considered / removed](docs/design.md#considered--removed) section.

**Resilient parallel calls to external providers.** SourcingService fans out to multiple store
providers concurrently, each raced against its own timeout, so one slow or failing provider
never blocks the others — see `SourcingAggregatorService`. Confirmed per-ingredient pricing
(Kroger) returns empty rather than a guess when nothing is found; the general nearby-stores
lookup (Google Places) falls back to a mock provider only when every real provider comes back
empty, so a transient outage degrades gracefully instead of returning nothing.

**Two real authorization bugs found and fixed.** Early versions let a caller pass an arbitrary
`userId` in the URL to read or write *any* user's pantry, and let a caller set `AuthorId`
directly on a recipe-creation request — both classic client-supplied-identity spoofing bugs.
Both were fixed the same way: every service now resolves the caller's identity from the
validated token's `sub` claim (via a shared `ICurrentUserResolver`/`/api/users/me` JIT-provision
flow) instead of trusting anything the client sends. Verified live against real Auth0-issued
tokens, not just at the unit-test level.

**Real distributed tracing, not a diagram of it.** Every service is instrumented with
OpenTelemetry; traces flow through Jaeger and metrics through Prometheus/Grafana in local dev,
including QStash's async webhook deliveries, which pick up the publisher's `traceparent` header
so a consumer span nests under the original request trace instead of starting a disconnected one.

## Getting started

```powershell
.\start-all.ps1
```

This starts QStash's local dev server, all four .NET services, and the web-client, each in its
own PowerShell window:

- QStash (local dev) — http://127.0.0.1:8080
- RecipeService — http://localhost:5081/swagger
- PantryService — http://localhost:5082/swagger
- SourcingService — http://localhost:5084/swagger
- Gateway (GraphQL) — http://localhost:5085/graphql
- web-client — http://localhost:5173

`.\stop-all.ps1` closes everything it started.

### What you need

- .NET 8 SDK, Node.js
- A Postgres database (Neon or local) for RecipeService and PantryService
- A Redis instance for SourcingService's cache
- An Auth0 tenant (an SPA app for web-client, an API for the backend services, and an `admin`
  role claim on your own user if you want to create/edit recipes and ingredients)
- Kroger API credentials (Certification environment for local dev) and a Google Places API key

Connection strings and API keys are left blank in each service's `appsettings.json` by design —
set them via `dotnet user-secrets set` or environment variables; `web-client/.env.example` covers
the frontend's Auth0 config. See [`docs/SETUP.md`](docs/SETUP.md) for the full account-by-account
walkthrough (Auth0 tenant/API/role claims, Neon, Kroger/Google API signup, QStash) — it's not
repeated here.

For tracing/metrics (optional, dev-only):

```powershell
docker compose -f docker-compose.observability.yml up
```

Jaeger UI: http://localhost:16686 · Prometheus: http://localhost:9090 · Grafana: http://localhost:3000

### Try it

Open http://localhost:5173, log in via Auth0, add pantry items, browse recipes (ranked by how
much of each you can already make), open one to see real store pricing for what you're missing,
and scale servings up or down. An `admin`-role account can also create, edit, and delete recipes
and ingredients.

To call a backend service's REST API directly instead, use its Swagger UI's **Authorize** button
with a valid Auth0 access token — every endpoint except health checks and the QStash webhook
requires one.

### Tests

```bash
dotnet test
```

RecipeService and PantryService have controller/service tests against mocks, plus repository
tests that spin up a throwaway Postgres container via Testcontainers (needs a reachable Docker
daemon). SourcingService doesn't have a test project yet.

## Current status

**Working end-to-end:** recipe browsing/creation/editing (admin), pantry tracking, "what can I
cook" recipe matching, real Kroger pricing and Google Places store lookups, servings scaling,
Auth0 login with role-based write access, and full distributed tracing/metrics in local dev.
Production runs on Render against Kroger's real Production API.

**Explicitly not built:**
- **gRPC between services** — internal calls are plain REST over `HttpClient`; sufficient at
  this scale, gRPC remains a possible follow-up.
- **Temporal workflow orchestration** — the multi-step "find this ingredient" flow (pantry →
  stores) doesn't exist as an orchestrated workflow, hand-rolled or otherwise.
- **On-call/incident-review simulation** — no deliberate fault-injection exercise or written
  postmortem yet.
- **S3-style image storage** — `recipes.image_url` is a plain string column with no upload
  pipeline behind it.
- The `ingredient.missing` event is published and consumed, but the consumer currently just logs
  it — no downstream action (auto-searching stores) is wired up yet.
- Production drops the observability stack and QStash for simplicity — see
  [`docs/design.md`](docs/design.md) for that tradeoff and what it costs.
- Ranked ingredient substitutions — built on a Neo4j graph, then removed; see design.md's
  [Considered / removed](docs/design.md#considered--removed) section.