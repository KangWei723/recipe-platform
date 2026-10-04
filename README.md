# Larder

Larder is a pantry-aware recipe app: it doesn't just store recipes, it knows what's actually in
your kitchen, tells you which recipes you can cook right now, and — for what you're missing —
looks up real nearby stores with real prices. Recipes carry a hero image, a per-step image, tips,
and a pairing note, with admin image upload (not just a pasted URL) going straight to Cloudinary.
It's built as a distributed system on purpose: four independently deployable .NET services
behind a GraphQL gateway, plus a React web-client, designed to demonstrate production-grade
backend engineering (service boundaries, resilience, observability, real auth) rather than to be
a CRUD demo with extra steps.

See [`docs/design.md`](docs/design.md) for the full architecture rationale, data model, and
decision history — including what was tried, what changed, and why.

## Architecture

```
web-client (React)  →  Gateway (GraphQL)  →  RecipeService, PantryService, SourcingService
```

| Service | Port | Owns | What it does |
|---|---|---|---|
| **Gateway** | `:5085` | — | GraphQL API (HotChocolate) that aggregates the three backend services over REST, plus one plain REST endpoint (`POST /api/images/upload`) that passes an admin's image upload through to RecipeService. Forwards the caller's bearer token downstream rather than validating it itself — each service enforces its own auth. |
| **RecipeService** | `:5081` | `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`, `users` (Postgres) | Recipes (with a hero image, per-step images, tips, and a pairing note), steps, the shared ingredient catalog, and user identity (JIT-provisioned from Auth0 tokens on first request). Also ranks recipes by how many of a given ingredient list they match, and validates/uploads admin-submitted recipe images to Cloudinary. |
| **PantryService** | `:5082` | `pantry_items` (Postgres) | Per-user pantry inventory (presence-only — has an ingredient or doesn't). Calls RecipeService for ingredient/recipe data rather than joining across databases, and publishes an `ingredient.missing` event when a recipe needs something you don't have. |
| **SourcingService** | `:5084` | — | "Where can I buy this" lookups: confirmed per-ingredient pricing via Kroger, general nearby stores via Google Places — run in parallel with per-provider timeouts, Redis-cached. |
| **web-client** | `:5173` | — | React + urql GraphQL client, Auth0 login. The intended way to use the app end-to-end. |

Every backend service validates Auth0 JWT bearer tokens itself and requires one by default —
health checks and the QStash webhook endpoint (which authenticates via its own HMAC signature
check instead) are the only anonymous routes. There's no dev-mode auth bypass; running this
locally requires a real Auth0 tenant. Recipe and ingredient writes, and image uploads, additionally
require an `admin` role claim on the token, enforced both at the Gateway (for a clean error) and
again at RecipeService (the real boundary).

## Tech stack

- **Backend**: C# / ASP.NET Core (.NET 8), four independently deployable services
- **API**: GraphQL (HotChocolate) at the Gateway, REST internally between Gateway and services
- **Data**: PostgreSQL (Neon) for relational data, Redis for sourcing-result caching
- **Media**: Cloudinary for recipe image hosting, server-validated upload (file-signature and size checks) from an admin-only endpoint
- **Events**: Upstash QStash (signed webhook delivery) for the `ingredient.missing` event
- **Auth**: Auth0 (JWT bearer tokens, role-based authorization, JIT user provisioning)
- **Frontend**: React 19 + Vite, urql, Tailwind v4 + shadcn/ui, Auth0 SPA SDK
- **External APIs**: Kroger (product/price search) and Google Places (store lookup)
- **Observability**: OpenTelemetry distributed tracing, Prometheus metrics, Grafana dashboards, Jaeger UI (local/dev)

## Technical highlights

**A real .NET 8 behavior, found by running the endpoint, not by reading the docs.** Gateway's
image-upload route 500'd on every request with "contains anti-forgery metadata, but a middleware
was not found" — .NET 8 attaches antiforgery metadata to any endpoint that binds a file upload
regardless of whether antiforgery middleware is registered anywhere in the app, which Gateway
never does. Fixed with `.DisableAntiforgery()` and a comment explaining why it's safe here
(bearer-token auth, not cookies — nothing for a forged request to ride). Full writeup in
[`docs/design.md`](docs/design.md#image-hosting-pasted-url--cloudinary-upload--upload-only).

**A schema/model drift bug, caught by a real user, not the test suite.** A migration added
`recipes.tips` as a nullable column while the C# model declared it non-nullable; every existing
row crashed the recipe list on load. Every repository test passed anyway, because every one of
them creates recipes through the same EF model that always supplies a non-null value — none
exercised a row that predated the column. Fixed with a backfill, `NOT NULL DEFAULT`, and a new
test that inserts a row via raw SQL bypassing the EF model entirely, to prove the gap is actually
closed. Full writeup in
[`docs/design.md`](docs/design.md#recipe-content-tips-pairing-and-per-step-images).

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
- A Cloudinary account (free tier is enough) for RecipeService's image upload
- An Auth0 tenant (an SPA app for web-client, an API for the backend services, and an `admin`
  role claim on your own user if you want to create/edit recipes, ingredients, or upload images)
- Kroger API credentials (Certification environment for local dev) and a Google Places API key

Connection strings and API keys are left blank in each service's `appsettings.json` by design —
set them via `dotnet user-secrets set` or environment variables; `web-client/.env.example` covers
the frontend's Auth0 config. See [`docs/SETUP.md`](docs/SETUP.md) for the full account-by-account
walkthrough (Auth0 tenant/API/role claims, Neon, Cloudinary, Kroger/Google API signup, QStash) —
it's not repeated here.

### Configure secrets

Each service reads its own keys from `dotnet user-secrets` (or the matching environment
variable); `appsettings.json` only ever ships blank placeholders for these.

| Service | Keys |
|---|---|
| **RecipeService** | `ConnectionStrings:RecipeDb`, `Auth0:Domain`/`Audience`, `Cloudinary:CloudName`/`ApiKey`/`ApiSecret` (and optionally `Cloudinary:RestrictImageUrlsToOwnCloud`, off by default) |
| **PantryService** | `ConnectionStrings:PantryDb`, `Auth0:Domain`/`Audience`, `QStash:Token` |
| **SourcingService** | `Kroger:Certification:ClientId`/`ClientSecret` (and the `Production` equivalents for a real deployment), `GooglePlaces:ApiKey`, `Redis:ConnectionString`, `Auth0:Domain`/`Audience` |
| **Gateway** | `Auth0:Domain`/`Audience`, `Cors:AllowedOrigins` (for a deployed frontend origin) |
| **web-client** | `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`, `VITE_AUTH0_AUDIENCE`, `VITE_GATEWAY_URL` — in `.env.local`, not user-secrets (see `.env.example`) |

For tracing/metrics (optional, dev-only):

```powershell
docker compose -f docker-compose.observability.yml up
```

Jaeger UI: http://localhost:16686 · Prometheus: http://localhost:9090 · Grafana: http://localhost:3000

### Try it

Open http://localhost:5173, log in via Auth0, add pantry items, browse recipes (ranked by how
much of each you can already make), open one to see real store pricing for what you're missing,
and scale servings up or down (a plain linear scale — see
[`docs/design.md`](docs/design.md#servings-scaling-client-side-and-deliberately-simple) for what
that doesn't handle). An `admin`-role account can also create and edit recipes and ingredients,
including uploading a hero image and per-step images, adding tips, and setting a pairing note.

To call a backend service's REST API directly instead, use its Swagger UI's **Authorize** button
with a valid Auth0 access token — every endpoint except health checks and the QStash webhook
requires one.

### Tests

```bash
dotnet test
```

RecipeService (89 tests) and PantryService (9 tests) have controller/service tests against
mocks, repository tests that spin up a throwaway Postgres container via Testcontainers (needs a
reachable Docker daemon), and — for the admin-only image-upload endpoint — a
`WebApplicationFactory`-based test that exercises the real ASP.NET Core authorization pipeline
with a fake authentication handler standing in for Auth0, rather than calling the controller
action directly the way every other controller test does. SourcingService doesn't have a test
project yet.

The Testcontainers-backed repository tests build their schema from the EF model
(`RecipeDbContext.OnModelCreating`, via `EnsureCreatedAsync`), not from `init-db/01-schema.sql` —
this project has no EF Core migrations, so those two descriptions of the same schema are kept in
sync by hand rather than generated from one source of truth. That gap caused a real bug once
(see [`docs/design.md`](docs/design.md#recipe-content-tips-pairing-and-per-step-images)): a
migration added a column as nullable while the EF model declared it non-nullable, and nothing in
the test suite caught it because every test writes data through the same model that was
(correctly) non-nullable.

## Current status

**Working end-to-end:** recipe browsing/creation/editing (admin) with a hero image, per-step
images, tips, and a pairing note; admin image upload to Cloudinary with server-side validation;
drag-to-reorder steps with images that move with them; pantry tracking; "what can I cook" recipe
matching; real Kroger pricing and Google Places store lookups; client-side servings scaling;
Auth0 login with role-based write access; and full distributed tracing/metrics in local dev.
Production runs on Render against Kroger's real Production API.

**Explicitly not built:**
- **gRPC between services** — internal calls are plain REST over `HttpClient`; sufficient at
  this scale, gRPC remains a possible follow-up.
- **Temporal workflow orchestration** — the multi-step "find this ingredient" flow (pantry →
  stores) doesn't exist as an orchestrated workflow, hand-rolled or otherwise.
- **On-call/incident-review simulation** — no deliberate fault-injection exercise or written
  postmortem yet.
- **Cleanup for orphaned Cloudinary assets** — removing or replacing an image never deletes the
  old file from Cloudinary.
- **Recipe drafts, cook history, and a shopping list** — "missing ingredients" is computed on
  demand per recipe, never accumulated into a list; there's no draft state and no history.
- **Multi-language recipe content** — every text field is single-language, plain text.
- The `ingredient.missing` event is published and consumed, but the consumer currently just logs
  it — no downstream action (auto-searching stores) is wired up yet.
- Production drops the observability stack and QStash for simplicity — see
  [`docs/design.md`](docs/design.md) for that tradeoff and what it costs.
- Ranked ingredient substitutions — built on a Neo4j graph, then removed; see design.md's
  [Considered / removed](docs/design.md#considered--removed) section.