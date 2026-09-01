# Larder — Design Doc

## Overview

Larder is a pantry-aware recipe app: recipes, a per-user pantry, ranked "what can I cook right
now" matching, ingredient substitutions, and real grocery pricing/store lookup for whatever
you're missing. It started as a solo portfolio project meant to demonstrate distributed-systems
engineering — service boundaries, partial-failure handling, concurrency, observability, and
event-driven design — with the recipe/pantry domain as the vehicle rather than the point.

That changed in August 2026: a small group of real people started using it. That raised the bar
on several things that had been "good enough for a demo" — real authentication, real per-user
data isolation, and reliability as a goal in its own right rather than something to demonstrate
on paper — and drove most of the decisions recorded below.

This doc covers the architecture as it stands, the reasoning behind the choices that shaped it
(including ones that were later reversed or abandoned), and an honest accounting of what's
actually built versus what was planned. It was built with extensive use of
[Claude Code](https://claude.com/claude-code) throughout — see the README for more on that.

## System architecture

```
web-client (React + urql)
        │
        ▼
    Gateway (GraphQL, HotChocolate)
        │  forwards the caller's bearer token to each service; doesn't validate it itself
        ├──────────────┬──────────────────┬──────────────────┐
        ▼              ▼                  ▼                  ▼
  RecipeService   PantryService   SubstitutionService   SourcingService
  (Postgres)      (Postgres)      (Neo4j)               (Kroger + Google Places, Redis-cached)
```

- **RecipeService** — recipes, steps, the shared ingredient catalog, and user identity. Owns
  Postgres tables `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`, `users`.
  Also computes recipe-matching ("given these ingredient ids, rank recipes by how much of each
  you already have") server-side, since it already loads every recipe's ingredients to build the
  plain recipe list — doing that ranking anywhere else would mean an expensive extra round trip.
- **PantryService** — per-user, presence-only ingredient inventory (owns `pantry_items`). Calls
  RecipeService's API for ingredient/recipe data rather than joining across service databases,
  and publishes an `ingredient.missing` event when a recipe needs something the caller doesn't
  have.
- **SubstitutionService** — ranked ingredient-substitution lookups backed by a Neo4j graph (see
  [Substitution model](#substitution-model-relational-table--neo4j-graph) below).
- **SourcingService** — "where can I buy this" lookups. Fires parallel calls to multiple store
  providers (Kroger for confirmed per-ingredient pricing, Google Places for general nearby
  stores), each raced against its own timeout so one slow provider never blocks the others,
  results cached in Redis.

Each service owns its schema exclusively — there is no shared database and no cross-service SQL
join. If Pantry needs an ingredient's name, it calls Recipe's API. This constraint is deliberate:
it forces real API design at the service boundary instead of a distributed system that quietly
behaves like one big database.

**Cross-cutting infrastructure:**

- **QStash** (Upstash-hosted, dev-only — see [Production scope](#production-scope-what-ships-to-render)) —
  decouples detection from action. `ingredient.missing` is published by PantryService and
  delivered via signed webhook to both SubstitutionService and SourcingService. Both handlers
  currently just log the event; no downstream action (auto-searching stores, suggesting a
  substitute) is wired up yet.
- **Observability** (dev-only) — OpenTelemetry tracing across every service, Prometheus metrics,
  Grafana dashboards and alerting rules. QStash's async webhook deliveries pick up the
  publisher's `traceparent` header, so a consumer span nests under the original request's trace
  instead of starting a disconnected one.
- **Auth0** — every backend service validates JWT bearer tokens itself (issuer = tenant domain,
  audience `https://recipemate.api`), with an authenticated-by-default fallback policy —
  endpoints opt **out** with `[AllowAnonymous]` (health checks, the two QStash webhook
  controllers, which keep their own HMAC check) rather than opting in, so a newly added endpoint
  is locked down unless someone deliberately opens it. Recipe/ingredient writes additionally
  require an `admin` role, read from a custom claim (`https://recipemate.api/roles`) since Auth0
  puts role names there rather than in the claim ASP.NET Core's built-in `RequireRole()` expects
  — enforced via an explicit `RequireAssertion` policy, at both the Gateway (for a clean GraphQL
  error) and RecipeService (the real boundary).

## API design

- **External** — GraphQL gateway (HotChocolate), the web-client's single entry point:
  - Queries: `recipe(id)`, `recipes`, `ingredients`, `units`, `ingredientCategories`,
    `pantryItems`, `recipeMatches(ingredientIds)`, `confirmedStoreOffer(ingredientName, lat, lng)`,
    `nearbyStoresGeneral(lat, lng)`, plus a nested `RecipeIngredient.substitutions` resolver.
  - Mutations: `upsertPantryItem`, `removePantryItem`, and admin-only `createRecipe`,
    `updateRecipe`, `deleteRecipe`, `createIngredient`, `updateIngredient`, `deleteIngredient`.
  - None of the pantry or recipe-authoring operations take a caller/user id argument — identity
    always comes from the forwarded bearer token, never from a client-supplied value (see
    [Authentication and authorization](#authentication-and-authorization-for-real-users) below).
- **Internal** — plain REST over `HttpClient` between Gateway and each backend service. gRPC was
  the original plan; see [Internal service calls](#internal-service-calls-grpc-planned--rest).
- **Events** — QStash webhook delivery, e.g. `ingredient.missing`; see
  [Event bus](#event-bus-kafka--qstash) below.

## Data model

### Relational core (Postgres, split by service ownership)

- `users(id, email, name, auth0_sub, created_at)` — RecipeService
- `recipes(id, author_id FK, title, description, servings, prep_time_min, cook_time_min, image_url, created_at)` — RecipeService
- `recipe_steps(id, recipe_id FK, step_number, instruction, timer_seconds)` — RecipeService
- `ingredients(id, name, category, default_unit)` — RecipeService (shared catalog; `category` and `default_unit` are validated against curated enums, not free text)
- `recipe_ingredients(id, recipe_id FK, ingredient_id FK, quantity, unit, optional)` — RecipeService
- `pantry_items(id, user_id FK, ingredient_id FK, updated_at)` — PantryService (presence-only: has the ingredient or doesn't, no quantity/unit/expiry tracking)
- `stores(id, place_id, name, address, lat, lng)` — SourcingService (cached from Google Places)
- `ingredient_prices(id, ingredient_id FK, store_id FK, price, currency, observed_at)` — SourcingService

### Substitution graph (Neo4j, owned by SubstitutionService)

- Nodes: `Ingredient(name, category)`
- Relationship: `SUBSTITUTES_FOR(ratio, contexts[], confidence)` — directional and weighted

Example query — "what can I use instead of butter for baking, ranked by confidence":

```cypher
MATCH (b:Ingredient {name: "Butter"})-[s:SUBSTITUTES_FOR]->(alt:Ingredient)
WHERE "baking" IN s.contexts
RETURN alt.name, s.ratio, s.confidence
ORDER BY s.confidence DESC
```

Why this shape, and how it replaced an earlier relational design, is covered in
[Substitution model](#substitution-model-relational-table--neo4j-graph) below.

## Key decisions

The sections below are kept roughly in the order the decisions were made, including the ones
that were later reversed — the history is the point as much as the current state is.

### Event bus: Kafka → QStash

Kafka was the original plan for the `ingredient.missing` event. It was replaced with Upstash
QStash: a hosted, HTTP-based message queue that delivers events as signed webhook calls instead
of requiring a broker cluster to run and operate. For a system at this scale, Kafka's operational
overhead (partitions, consumer groups, a cluster to keep alive) bought nothing that QStash's
simpler delivery-with-signature-verification model didn't already cover, and QStash's local dev
server gave deterministic local testing without standing up real infrastructure. Two
signature-verification bugs were found and fixed by testing against real QStash deliveries
rather than trusting the SDK's documented contract at face value.

### Substitution model: relational table → Neo4j graph

Substitution logic shipped first as a plain Postgres table
(`ingredient_id, substitute_id, ratio, context`) — a deliberate "start simple, migrate when it's
outgrown" sequence rather than reaching for a graph database on day one. It was then retired in
favor of a Neo4j-backed graph once the model needed things a join table can't express cleanly:

- **Directionality/asymmetry** — applesauce can replace butter in a muffin recipe; butter can't
  replace applesauce in a smoothie. A symmetric join table can't represent this without
  duplicating rows and tracking direction by convention.
- **Context lives on the edge** — the same ingredient pair can have different ratios depending on
  cooking method (frying vs. baking), which belongs naturally on a weighted, labeled relationship
  rather than as extra table columns multiplying the row count.
- **Transitive lookups are cheap** — "what else substitutes for something that substitutes for
  X" is a 2-hop graph traversal, not a recursive CTE.

The schema and an example query are in [Data model](#data-model) above.

### Authentication and authorization for real users

Nothing in the platform validated a caller's identity before this. Once real users were in the
picture, an audit of the existing code turned up several places where that mattered more than
"someday":

- **No authentication existed anywhere.** *Fixed:* all four backend services now validate Auth0
  JWT bearer tokens via a shared `Auth` project, authenticated-by-default (see
  [System architecture](#system-architecture) above). Verified end-to-end with a real
  Auth0-issued token, not just config-level checks.
- **Pantry data could be read or written for any user.** The `{userId}` route parameter on every
  pantry endpoint (`api/pantry/users/{userId}/...`) was taken directly from the URL — any
  authenticated caller could substitute a different id and access someone else's pantry. *Fixed:*
  the route parameter is gone; every action now resolves the caller's numeric id from the
  token's `sub` claim via `ICurrentUserResolver` (with a short in-memory cache to avoid a
  cross-service round trip on every request). Verified live: a real token wrote a pantry item
  under its own resolved id and read it back correctly, with no client-supplied identity
  anywhere in the request.
- **Recipe creation trusted a client-supplied author id.** `CreateRecipeRequest.AuthorId` was a
  plain request field — the same spoofing shape as the pantry bug above, letting a caller
  attribute a recipe to any user id. *Fixed:* `AuthorId` is now derived the same way, from the
  resolved current user.
- **No service had a durable link between a Postgres user row and an Auth0 identity.** *Fixed:*
  RecipeService's `users` table gained a unique, nullable `auth0_sub` column; `POST
  /api/users/me` resolves-or-JIT-provisions a user from the token's `sub` claim (with a
  retry-on-unique-violation for the concurrent-first-login race), and every other service goes
  through this same endpoint rather than inventing its own identity source. A Post-Login Action
  was added in Auth0 to populate real `email`/`name` claims on the token, so JIT-provisioned
  users get real values instead of placeholders.
- **The `ingredient.missing` event carries a `UserId` that nothing uses.** Both consumers are
  still logging stubs (see [System architecture](#system-architecture) above), so there's no
  per-user scoping to get wrong yet — but it will matter once they do real work.

Two of the items above (pantry access and recipe authorship) were genuine authorization
vulnerabilities in a live sense: an authenticated user could act as a different user, not just a
config gap. Both are called out specifically in the README's technical highlights.

Once auth existed, a second pass added role-gated writes: recipe and ingredient
create/update/delete now require an `admin` role claim, enforced at both the Gateway and
RecipeService (see [System architecture](#system-architecture) above).

### Kroger environment: Certification → Production

Kroger's integration originally targeted only the Certification (sandbox) environment.
`KrogerOptions` now has a `Kroger:Environment` switch (`Certification`/`Production`), each with
its own base URL and credentials — local dev defaults to Certification
(`api-ce.kroger.com`), and the production deployment sets `Kroger__Environment=Production` plus
production credentials as environment variables. Verified end-to-end against the real
`api.kroger.com` Production API: token refresh, location lookup, and product search all
succeeded with real store and price data returned.

### CORS: permissive → allowlist

Gateway's CORS policy was originally wide open. It's now a config-driven allowlist
(`Cors:AllowedOrigins`), defaulting to common local SPA dev ports (the web-client's
`localhost:5173` among them) — the real deployed frontend origin gets added once one exists
outside local dev.

### Internal service calls: gRPC (planned) → REST

gRPC between Gateway and the backend services was part of the original plan. It was never built;
plain REST over `HttpClient` shipped instead and has been sufficient at this system's scale and
call volume. gRPC remains a reasonable follow-up if internal latency or throughput ever demands
it, but there's no evidence yet that it would.

### Workflow orchestration: Temporal — evaluated, not built

Temporal was planned to orchestrate a multi-step "find this ingredient" flow: check
substitutions → check pantry → search stores → geocode. That flow doesn't exist yet in any form,
hand-rolled or orchestrated, so there was nothing yet to justify bringing in a workflow engine
for. Recorded here as a deliberate scope decision rather than an oversight.

### Image storage: S3 — planned, not built

Recipe images were planned to go through S3-compatible object storage. `recipes.image_url` is
currently a plain string column with no upload pipeline behind it.

### Production scope: what ships to Render

Local dev and demo usage keep the full distributed architecture. Production, deployed on Render,
is intentionally simplified:

- Neo4j ships to production — the substitution graph is core product behavior, not simplified away.
- QStash and the full observability stack (OpenTelemetry/Jaeger/Prometheus/Grafana) stay
  **dev-only** and do not run in production.

This is a real tradeoff, not a free simplification: dropping the observability stack from
production means no tracing/metrics visibility into real user-facing incidents — which is
exactly what reliability work depends on. At minimum, some production-friendly signal (structured
logs plus a hosted logging/APM tier, even a lightweight one) belongs in production even though
the full local Jaeger/Prometheus/Grafana stack doesn't come along. This hasn't been built yet;
it's the most honest gap between "reliability now matters" and what's actually deployed.

### Original phase plan (retrospective)

For context, this was the phase plan written before any code existed. Where reality diverged —
QStash instead of Kafka, REST instead of gRPC, no Temporal — see the decisions above rather than
this section, which is kept as-written for the record.

- **Phase 1 — MVP.** Recipe + Pantry services, Postgres, a plain lookup table for substitutions,
  Dockerized. Goal: a working end-to-end app.
- **Phase 2 — Distributed systems layer.** Split out Substitution and Sourcing as separate
  services, add inter-service RPC, wire an event bus for `ingredient.missing`, integrate Google
  Places with parallel async calls and timeout aggregation, add Redis caching, add
  OpenTelemetry/Prometheus/Grafana, migrate substitution logic to a graph database.
  **Phase 3 — Polish.** Workflow orchestration for the sourcing flow, infra-as-code + CI/CD, a
  deliberate fault-injection exercise with a written postmortem, architecture documentation.

## Current status

**Working today:** recipe browsing/creation/editing (admin-gated), pantry tracking, ranked
recipe matching against pantry contents, ranked ingredient substitutions, real Kroger pricing and
Google Places store lookups run in parallel with timeout/fallback handling, servings scaling,
Auth0 login with role-based write access and JIT user provisioning, and full OpenTelemetry
tracing/Prometheus/Grafana metrics in local dev. Production is deployed on Render against
Kroger's real Production API.

**Not built, and not pretending otherwise:**

- gRPC between services (see [above](#internal-service-calls-grpc-planned--rest))
- Temporal workflow orchestration (see [above](#workflow-orchestration-temporal--evaluated-not-built))
- S3-style recipe image storage (see [above](#image-storage-s3--planned-not-built))
- A deliberate on-call/incident-review simulation — alerting rules, fault injection, and a
  written postmortem were planned as a way to manufacture on-call experience solo, but weren't
  executed
- Any downstream action on the `ingredient.missing` event — both consumers currently just log it
- Unit conversion in the missing-ingredients diff — it compares quantities directly and assumes
  the pantry item's unit already matches the recipe's
- Production observability — see [Production scope](#production-scope-what-ships-to-render) above
  for the honest tradeoff this represents
