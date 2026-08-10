# Recipe Platform

A recipe app — browse/create recipes, track a pantry, get ingredient substitutions, find nearby
stores that sell what you're missing — built as a distributed system: five independently
deployable .NET services behind a GraphQL gateway, plus a React web-client. See
[`docs/design.md`](docs/design.md) for the full architecture rationale, data model, and an
explicit list of what's still just planned vs. actually built.

## Services

| Service | Port | Owns | Notes |
|---|---|---|---|
| **Gateway** | `:5085` | — | GraphQL (HotChocolate), aggregates the backend services over REST. Doesn't validate the caller's token itself — forwards it downstream and lets each service enforce its own auth. |
| **RecipeService** | `:5081` | `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`, `users` (Postgres) | Recipes, steps, ingredient catalog, and user identity (JIT-provisioned from Auth0 tokens). |
| **PantryService** | `:5082` | `pantry_items` (Postgres) | Per-user pantry inventory. Calls RecipeService for ingredient/recipe data, never joins across databases. Publishes the `ingredient.missing` QStash event. |
| **SubstitutionService** | `:5083` | Substitution graph (Neo4j) | Ingredient substitution lookups. Also consumes `ingredient.missing` via a QStash webhook (currently just logs it). |
| **SourcingService** | `:5084` | — | "Where can I buy this" lookups against Kroger + Google Places, aggregated with a timeout, Redis-cached. Also consumes `ingredient.missing` via a QStash webhook (currently just logs it). |
| **web-client** | `:5173` | — | React + urql GraphQL client, Auth0 SPA login. The intended way to use the app end-to-end. |

Every backend service validates Auth0 JWT bearer tokens itself and requires one by default
(health checks and the QStash webhook endpoints are the only anonymous routes). There's no
dev-mode auth bypass — you need a real Auth0 tenant to run this locally.

## Prerequisites

- .NET 8 SDK
- Node.js (for `web-client` and the QStash local dev CLI, run via `npx`)
- A Postgres database reachable from RecipeService and PantryService (Neon or local — whatever
  you point `ConnectionStrings:RecipeDb`/`PantryDb` at)
- A Neo4j instance (AuraDB or local) for SubstitutionService
- A Redis instance for SourcingService's cache
- An Auth0 tenant: an SPA application (for web-client) and an API (audience
  `https://recipemate.api`, validated by every backend service)
- Kroger API credentials (Certification environment for local dev) and a Google Places API key,
  for SourcingService

## Configure secrets

Connection strings and API keys are blank in each service's `appsettings.json` by design — set
them via `dotnet user-secrets set` (each service has its own `<UserSecretsId>`) or environment
variables. Roughly:

- **RecipeService**: `ConnectionStrings:RecipeDb`, `Auth0:Domain`, `Auth0:Audience`
- **PantryService**: `ConnectionStrings:PantryDb`, `Auth0:Domain`, `Auth0:Audience`,
  `RecipeService:BaseUrl`, `QStash:BaseUrl`/`Token`/`CurrentSigningKey`/`NextSigningKey`
- **SubstitutionService**: `Neo4j:Uri`/`Username`/`Password`, `Auth0:Domain`/`Audience`,
  `QStash:CurrentSigningKey`/`NextSigningKey`/`WebhookUrl`
- **SourcingService**: `Kroger:*` (see `KrogerOptions`), `GooglePlaces:ApiKey`,
  `Redis:ConnectionString`, `Auth0:Domain`/`Audience`,
  `QStash:CurrentSigningKey`/`NextSigningKey`/`WebhookUrl`
- **Gateway**: no secrets — just each service's `BaseUrl` and `Cors:AllowedOrigins`, both already
  defaulted for local dev
- **web-client**: copy `web-client/.env.example` to `web-client/.env.local` and fill in your
  Auth0 SPA client ID/domain/audience

The exact keys for each service are whatever's bound in that service's `Program.cs` — check
there or the corresponding `appsettings.json` if something's missing.

## Run everything

```powershell
.\start-all.ps1
```

Launches QStash's local dev server, all five .NET services, and the web-client, each in its own
PowerShell window:

- QStash (local dev) — http://127.0.0.1:8080
- RecipeService — http://localhost:5081/swagger
- PantryService — http://localhost:5082/swagger
- SubstitutionService — http://localhost:5083/swagger
- SourcingService — http://localhost:5084/swagger
- Gateway (GraphQL) — http://localhost:5085/graphql
- web-client — http://localhost:5173

Run `.\stop-all.ps1` to close everything it started.

For tracing/metrics (optional, dev-only):

```powershell
docker compose -f docker-compose.observability.yml up
```

Jaeger UI: http://localhost:16686 · Prometheus: http://localhost:9090 · Grafana: http://localhost:3000

## Database schema

RecipeService's and PantryService's `init-db/*.sql` scripts document their Postgres schemas —
run them against whatever instance you pointed the connection strings at. SubstitutionService's
Neo4j graph has no init script; its node/relationship shape is documented in `docs/design.md`.

## Try it

The intended path is through the web-client: run `.\start-all.ps1`, open
http://localhost:5173, log in via Auth0, create a recipe, add pantry items, and look up nearby
stores for something you're missing.

To call a backend service's REST API directly, open its Swagger UI and use the **Authorize**
button with a valid Auth0 access token (audience `https://recipemate.api`) — every endpoint
except health checks requires one.

## Tests

```bash
dotnet test
```

RecipeService and PantryService have controller/service tests against mocks, plus repository
tests that spin up a throwaway Postgres container via Testcontainers (needs a reachable Docker
daemon). SourcingService and SubstitutionService don't have test projects yet.

## Known limitations

- The `ingredient.missing` QStash event carries a `UserId`, but SubstitutionService's and
  SourcingService's webhook handlers currently just log it — nothing downstream acts on it yet.
- The missing-ingredients diff compares quantities directly with no unit conversion — it assumes
  the pantry item's unit matches the recipe's.
- Production (Render) runs without the observability stack or QStash — see "Production usage
  shift" in `docs/design.md` for the reasoning and open tradeoffs there.
