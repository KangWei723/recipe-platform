# Larder — Design Doc

## Purpose

Solo portfolio project built to demonstrate distributed systems, microservices, API design, concurrency, observability, and event-driven/workflow-orchestration skills for backend engineering roles. The product surface (a recipe app with ingredient substitution and "find it nearby" sourcing) is the vehicle; the architecture is the point.

## Production usage shift (2026-08-04)

**Read this before making further architectural decisions.**

The goal has expanded beyond a portfolio demo: a small group of real users will actually use this app. That changes what matters, on top of the qualification-mapping goals below:

- **Real auth is now required** — see the audit below; none currently exists.
- **Real data isolation between users is now required** — not just a schema column.
- **Reliability now matters** for its own sake, not just as something to demonstrate.

**Production deployment target:** Render, for the .NET services. Kroger's Production API is now available as a deployment target alongside the Certification environment, which remains what local dev points at (see flag #5 below — the code currently only knows about Certification).

**Decision — local dev/demo keeps the full distributed architecture; production is simplified:**
- Neo4j (substitution graph) ships to production, not simplified away.
- QStash and the full observability stack (OpenTelemetry/Jaeger/Prometheus/Grafana) stay **dev-only** and do not run in production.

**Flag — this is in tension with "reliability now matters":** dropping the observability stack from production means zero tracing/metrics visibility into real user-facing incidents, which is the exact thing reliability work depends on. Worth revisiting before launch: at minimum, structured logs plus some production-friendly uptime/error-rate signal (e.g. a hosted logging/APM tier, even a lightweight one) probably needs to exist in prod, even though the full local Jaeger/Prometheus/Grafana stack doesn't come along.

**What was already built assuming "demo only," audited against the current code, and needing reconsideration now:**

1. ~~**No authentication exists anywhere in the platform.**~~ **Resolved 2026-08-05**: all four backend services (Recipe/Pantry/Substitution/Sourcing) validate Auth0 JWT bearer tokens (issuer = tenant domain, audience = `https://recipemate.api`) via a shared `Auth` project (`Auth.AddAuth0Authentication`), with a global fallback policy requiring an authenticated user by default — endpoints opt **out** with `[AllowAnonymous]` (health checks, the two QStash webhook controllers, which keep their own HMAC check) rather than opting in, so newly added endpoints are locked down unless someone deliberately opens them. Gateway itself doesn't validate — it forwards the caller's Authorization header to every downstream call via `AuthHeaderForwardingHandler` and lets each backend service enforce its own auth. Verified end-to-end with a real Auth0-issued user token (see #2/#3 below), not just unit-level config checks.
2. ~~**PantryService's per-user isolation is enforced at the query level but not at the identity level.**~~ **Resolved 2026-08-05**: the `{userId}` route parameter is gone entirely (`api/pantry/users/{userId}` → `api/pantry`); every action resolves the caller's numeric id via `ICurrentUserResolver`, which reads the token's `sub` claim and calls RecipeService's `/api/users/me` (5-minute in-memory cache to avoid a cross-service round trip on every request). Verified live: a real token wrote a pantry item under the resolved numeric id and read it back correctly, with zero client-supplied identity anywhere in the request.
3. ~~**Only RecipeService has a `users` table**, and no service reads identity from a JWT claim...~~ **Resolved 2026-08-05**: RecipeService's `users` table gained an `auth0_sub` column (unique, nullable for pre-existing rows); `POST /api/users/me` resolves-or-JIT-provisions a user record from the validated token's `sub` claim (with a retry-on-unique-violation for the concurrent-first-login race), and every other service goes through this same endpoint rather than inventing its own identity source. `RecipesController.Create` was also fixed the same way — `AuthorId` used to be a client-supplied request field (the same spoofing pattern as Pantry's old bug); it's now derived from the resolved current user. A Post-Login Action was added 2026-08-09 to populate the `email`/`name` claims on the token, so JIT-provisioned users now get real values instead of placeholders.
4. **The QStash `ingredient.missing` event carries a `UserId` field, but it's inert.** Both SourcingService's and SubstitutionService's webhook handlers only log it — there's no per-user scoping of downstream action yet, since both handlers are still stubs. This will matter once they do real work.
5. ~~**Kroger integration currently targets the Certification environment** with Certification credentials only.~~ **Resolved 2026-08-04**: `KrogerOptions` now has a `Kroger:Environment` switch (`Certification`/`Production`), each with its own `BaseUrl`/`ClientId`/`ClientSecret`. Local dev defaults to Certification (`api-ce.kroger.com`); Render's production deployment sets `Kroger__Environment=Production` plus `Kroger__Production__ClientId`/`ClientSecret` as env vars. Verified end-to-end against the real `api.kroger.com` Production API (token refresh, location lookup, and product search all succeeded, real store/prices returned).
6. ~~**Gateway CORS is deliberately permissive**~~ **Resolved 2026-08-05**: replaced with a config-driven allowlist (`Cors:AllowedOrigins`), defaulting to common local SPA dev ports. The React web-client (`web-client/`, added 2026-08-09) runs on one of those defaults (`localhost:5173`) locally — add the real deployed origin once one exists.

## Qualification → architecture mapping

| Requirement | Where it shows up |
|---|---|
| Distributed systems experience | Multiple independently-deployable services communicating over the network, with partial-failure handling (timeouts, retries, circuit breaking) |
| Java/C# + OO design | Services in Java (Spring Boot) or C# (.NET). Strategy pattern for substitution algorithms, Factory for grocery-provider integrations, Repository pattern for data access |
| Multithreading/parallelism + observability | Parallel async calls to multiple store/pricing providers in the Sourcing service; OpenTelemetry tracing + Prometheus/Grafana across all services |
| Microservices, data modeling, API design | Recipe, Pantry, Substitution, Sourcing services; relational schema + graph schema; GraphQL (external) + gRPC (internal) |
| Communicating complex ideas | This doc, plus ADRs per major decision, plus a README with diagrams |
| On-call + incident reviews | Simulated: alerting rules, deliberate fault injection, written postmortem using a real incident-review template |
| Infra/platform experience | Docker, Terraform, CI/CD, optionally k8s |
| gRPC/GraphQL | External-facing GraphQL gateway (HotChocolate) is built. Internal gRPC was planned but not built — see [Considered, not implemented](#considered-not-implemented) |
| Cloud storage, data platform, event-driven, ontology, workflow orchestration | Neo4j substitution graph (ontology) is built; event-driven flow uses QStash (not Kafka — see [System architecture](#system-architecture)). S3 image storage and Temporal workflow orchestration were planned but not built — see [Considered, not implemented](#considered-not-implemented) |
| AI-assisted dev + tool vetting | Running log of AI coding tools used, where they helped vs. didn't |

## System architecture

**web-client** (React + urql, `web-client/`) → **GraphQL gateway** (HotChocolate, aggregates service calls) → four backend services:

- **Recipe service** — recipes, steps, ingredient catalog. Owns Postgres tables: `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`, `users`.
- **Pantry service** — per-user ingredient inventory. Owns `pantry_items`.
- **Substitution service** — ingredient substitution logic, backed by a Neo4j graph (see Data model below).
- **Sourcing service** — "where can I buy this" lookups. Fires parallel async calls to multiple providers (Google Places, Kroger), aggregates with a timeout. Caches results in Redis.

Cross-cutting infrastructure:

- **QStash event bus** (Upstash-hosted; dev-only, see production note above) — decouples detection from action. Example event: `ingredient.missing` (emitted by Pantry when a recipe requires something the user doesn't have), delivered via signed webhook to SubstitutionService and SourcingService. Both handlers currently only log the event (see flag #4 above) — no downstream action is wired up yet.
- **Observability stack** — OpenTelemetry tracing across every service, Prometheus metrics, Grafana dashboards + alerting rules (dev-only, see production note above).

Temporal workflow orchestration and S3-compatible image storage were part of the original plan but were never built — see [Considered, not implemented](#considered-not-implemented).

Each service owns its own schema — no shared database. If Pantry needs an ingredient's name, it calls Recipe's API rather than joining across schemas. This is the constraint that forces real API design instead of SQL joins across service boundaries.

## Considered, not implemented

Part of the original architecture plan (see qualification-mapping table above), never built. Recorded here so the plan/reality gap is explicit rather than silently dropped:

- **gRPC between services** — planned for internal Gateway↔service calls; built as plain REST over `HttpClient` instead (see API design below). REST has been sufficient at this scale; gRPC remains a possible follow-up if internal call volume/latency ever demands it.
- **Temporal workflow engine** — planned to orchestrate the multi-step "find this ingredient" flow (check substitution → check pantry → search stores → geocode). That flow doesn't exist yet in any form, hand-rolled or otherwise.
- **S3-compatible image storage** — planned for recipe images. `recipes.image_url` is a plain string column with no upload/storage pipeline behind it.

## Data model

### Relational core (Postgres, split by service ownership)

- `users(id, email, name, created_at)`
- `recipes(id, author_id FK, title, description, servings, prep_time_min, cook_time_min, image_url, created_at)` — Recipe service
- `recipe_steps(id, recipe_id FK, step_number, instruction, timer_seconds)` — Recipe service
- `ingredients(id, name, category, default_unit)` — Recipe service (shared catalog)
- `recipe_ingredients(id, recipe_id FK, ingredient_id FK, quantity, unit, optional)` — Recipe service
- `pantry_items(id, user_id FK, ingredient_id FK, updated_at)` — Pantry service (presence-only: has the ingredient or doesn't, no quantity/unit/expiry tracking)
- `stores(id, place_id, name, address, lat, lng)` — Sourcing service (cached from Google Places)
- `ingredient_prices(id, ingredient_id FK, store_id FK, price, currency, observed_at)` — Sourcing service

### Substitution graph (Neo4j, owned by Substitution service)

Nodes: `Ingredient(name, category)`
Relationship: `SUBSTITUTES_FOR(ratio, contexts[], confidence)` — directional and weighted.

Why a graph instead of a join table:
- **Directionality/asymmetry** — applesauce can replace butter in a muffin recipe; butter can't replace applesauce in a smoothie. A symmetric join table can't express this.
- **Context lives on the edge** — the same ingredient pair can have different ratios depending on cooking method (frying vs. baking).
- **Transitive lookups are cheap** — "what else substitutes for something that substitutes for X" is a 2-hop traversal, not a recursive CTE.

Example query — "what can I use instead of butter for baking, ranked by confidence":
```cypher
MATCH (b:Ingredient {name: "Butter"})-[s:SUBSTITUTES_FOR]->(alt:Ingredient)
WHERE "baking" IN s.contexts
RETURN alt.name, s.ratio, s.confidence
ORDER BY s.confidence DESC
```

~~**Open decision:** start the substitution logic as a plain Postgres table (`ingredient_id, substitute_id, ratio, context`) in Phase 1, and migrate to Neo4j in Phase 2 as a deliberate "outgrew the relational model" story — stronger interview narrative than starting with Neo4j from day one.~~ **Resolved**: took this path — the Postgres table shipped in Phase 1, then was retired in favor of the Neo4j-backed SubstitutionService above.

## API design

- **External**: GraphQL gateway (HotChocolate), single entry point for the web-client. Current surface:
  - Queries: `recipe(id)`, `recipes`, `ingredients`, `pantryItems`, `nearbyStores(ingredientName, lat, lng)`, plus nested resolvers `RecipeIngredient.substitutions` and `RecipeIngredient.nearbyStores(lat, lng)`.
  - Mutations: `createRecipe`, `upsertPantryItem`, `removePantryItem`.
  - Gateway doesn't validate the caller's token itself — it forwards the Authorization header to every downstream call and lets each backend service enforce its own auth (see #1 above).
- **Internal**: plain REST over `HttpClient` (Gateway ↔ Recipe/Pantry/Substitution/Sourcing) — gRPC was the original plan but was never built; see [Considered, not implemented](#considered-not-implemented).
- **Events**: QStash, e.g. `ingredient.missing` — not Kafka, see [System architecture](#system-architecture).

## Build order

*This is the original phase plan as written before Phase 1 started. Where reality diverged (QStash instead of Kafka, REST instead of gRPC, no Temporal), see [System architecture](#system-architecture) and [Considered, not implemented](#considered-not-implemented) above rather than this section.*

**Phase 1 — MVP (2–3 weeks)**
Recipe + Pantry services (Java/Spring Boot or C#/.NET), Postgres, simple REST or GraphQL, Dockerized, substitution as a plain lookup table. Goal: working end-to-end app.

**Phase 2 — Distributed systems layer (3–4 weeks)**
Split out Substitution and Sourcing as separate services, add gRPC between services, wire Kafka for `ingredient.missing`, integrate Google Places API with parallel async calls + timeout aggregation, add Redis caching, add OpenTelemetry + Prometheus + Grafana, migrate substitution logic to Neo4j.

**Phase 3 — Polish (2–3 weeks)**
Temporal workflow for the sourcing flow, Terraform + CI/CD, deliberate fault-injection exercise + written postmortem, ADRs, architecture README.

## On-call / incident review simulation

Since this is solo, on-call has to be manufactured deliberately:
1. Set up alerting rules in Grafana (e.g. p99 latency, error rate thresholds).
2. Run a chaos exercise — kill the Sourcing service mid-request, or throttle QStash delivery — and observe what breaks.
3. Write a postmortem: timeline, root cause, contributing factors, action items. Use a real incident-review template (e.g. Google SRE postmortem format).

## AI tool usage log

Keep a running note of which AI coding tools were used, where they helped, and where they didn't (e.g. "Claude Code scaffolded gRPC boilerplate quickly but the Temporal workflow retry semantics needed hand-tuning"). This maps directly to "actively seek out and vet new AI-driven development tools."

## Next step

Hand this file to Claude Code and start Phase 1: scaffold the Recipe and Pantry services, Postgres schema, and Docker setup.
