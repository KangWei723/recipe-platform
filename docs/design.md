# Recipe Platform — Design Doc

## Purpose

Solo portfolio project built to demonstrate distributed systems, microservices, API design, concurrency, observability, and event-driven/workflow-orchestration skills for backend engineering roles. The product surface (a recipe app with ingredient substitution and "find it nearby" sourcing) is the vehicle; the architecture is the point.

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
| gRPC/GraphQL | Internal service-to-service gRPC, external-facing GraphQL gateway |
| Cloud storage, data platform, event-driven, ontology, workflow orchestration | S3-compatible storage for images, Kafka for events, Neo4j substitution graph (ontology), Temporal for the multi-step "find this ingredient" workflow |
| AI-assisted dev + tool vetting | Running log of AI coding tools used, where they helped vs. didn't |

## System architecture

**Client app** → **GraphQL gateway** (aggregates service calls) → four backend services:

- **Recipe service** — recipes, steps, ingredient catalog. Owns Postgres tables: `recipes`, `recipe_steps`, `recipe_ingredients`, `ingredients`.
- **Pantry service** — per-user ingredient inventory. Owns `pantry_items`.
- **Substitution service** — ingredient substitution logic, backed by a Neo4j graph (see Data model below).
- **Sourcing service** — "where can I buy this" lookups. Fires parallel async calls to multiple providers (Google Places, grocery pricing APIs), aggregates with a timeout. Owns `stores`, `ingredient_prices`.

Cross-cutting infrastructure:

- **Kafka event bus** — decouples detection from action. Example event: `ingredient.missing` (emitted by Pantry when a recipe requires something the user doesn't have) triggers the Substitution/Sourcing flow asynchronously.
- **Temporal workflow engine** — orchestrates the multi-step "find this ingredient" flow: check substitution → check pantry → search stores → geocode → return results. Handles retries/timeouts declaratively instead of hand-rolled logic.
- **Observability stack** — OpenTelemetry tracing across every service, Prometheus metrics, Grafana dashboards + alerting rules.
- **S3-compatible storage** — recipe images, served via CDN.

Each service owns its own schema — no shared database. If Pantry needs an ingredient's name, it calls Recipe's API rather than joining across schemas. This is the constraint that forces real API design instead of SQL joins across service boundaries.

## Data model

### Relational core (Postgres, split by service ownership)

- `users(id, email, name, created_at)`
- `recipes(id, author_id FK, title, description, servings, prep_time_min, cook_time_min, image_url, created_at)` — Recipe service
- `recipe_steps(id, recipe_id FK, step_number, instruction, timer_seconds)` — Recipe service
- `ingredients(id, name, category, default_unit)` — Recipe service (shared catalog)
- `recipe_ingredients(id, recipe_id FK, ingredient_id FK, quantity, unit, optional)` — Recipe service
- `pantry_items(id, user_id FK, ingredient_id FK, quantity, unit, expiry_date, updated_at)` — Pantry service
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

**Open decision:** start the substitution logic as a plain Postgres table (`ingredient_id, substitute_id, ratio, context`) in Phase 1, and migrate to Neo4j in Phase 2 as a deliberate "outgrew the relational model" story — stronger interview narrative than starting with Neo4j from day one. Default: do this unless time pressure says otherwise.

## API design

- **External**: GraphQL gateway, single entry point for the client app.
- **Internal**: gRPC between services (Gateway ↔ Recipe/Pantry/Substitution/Sourcing).
- **Events**: Kafka topics, e.g. `ingredient.missing`, `recipe.viewed`.

## Build order

**Phase 1 — MVP (2–3 weeks)**
Recipe + Pantry services (Java/Spring Boot or C#/.NET), Postgres, simple REST or GraphQL, Dockerized, substitution as a plain lookup table. Goal: working end-to-end app.

**Phase 2 — Distributed systems layer (3–4 weeks)**
Split out Substitution and Sourcing as separate services, add gRPC between services, wire Kafka for `ingredient.missing`, integrate Google Places API with parallel async calls + timeout aggregation, add Redis caching, add OpenTelemetry + Prometheus + Grafana, migrate substitution logic to Neo4j.

**Phase 3 — Polish (2–3 weeks)**
Temporal workflow for the sourcing flow, Terraform + CI/CD, deliberate fault-injection exercise + written postmortem, ADRs, architecture README.

## On-call / incident review simulation

Since this is solo, on-call has to be manufactured deliberately:
1. Set up alerting rules in Grafana (e.g. p99 latency, error rate thresholds).
2. Run a chaos exercise — kill the Sourcing service mid-request, or throttle Kafka — and observe what breaks.
3. Write a postmortem: timeline, root cause, contributing factors, action items. Use a real incident-review template (e.g. Google SRE postmortem format).

## AI tool usage log

Keep a running note of which AI coding tools were used, where they helped, and where they didn't (e.g. "Claude Code scaffolded gRPC boilerplate quickly but the Temporal workflow retry semantics needed hand-tuning"). This maps directly to "actively seek out and vet new AI-driven development tools."

## Next step

Hand this file to Claude Code and start Phase 1: scaffold the Recipe and Pantry services, Postgres schema, and Docker setup.
