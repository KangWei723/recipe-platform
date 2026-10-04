# Larder — Design Doc

## Overview

Larder is a pantry-aware recipe app: recipes, a per-user pantry, ranked "what can I cook right
now" matching, and real grocery pricing/store lookup for whatever you're missing. It started as
a solo portfolio project meant to demonstrate distributed-systems
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
        ├──────────────┬──────────────────┐
        ▼              ▼                  ▼
  RecipeService   PantryService    SourcingService
  (Postgres)      (Postgres)       (Kroger + Google Places, Redis-cached)
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
  delivered via signed webhook to SourcingService. The handler currently just logs the event;
  no downstream action (auto-searching stores) is wired up yet.
- **Observability** (dev-only) — OpenTelemetry tracing across every service, Prometheus metrics,
  Grafana dashboards and alerting rules. QStash's async webhook deliveries pick up the
  publisher's `traceparent` header, so a consumer span nests under the original request's trace
  instead of starting a disconnected one.
- **Auth0** — every backend service validates JWT bearer tokens itself (issuer = tenant domain,
  audience `https://recipemate.api`), with an authenticated-by-default fallback policy —
  endpoints opt **out** with `[AllowAnonymous]` (health checks, the QStash webhook controller,
  which keeps its own HMAC check) rather than opting in, so a newly added endpoint
  is locked down unless someone deliberately opens it. Recipe/ingredient writes additionally
  require an `admin` role, read from a custom claim (`https://recipemate.api/roles`) since Auth0
  puts role names there rather than in the claim ASP.NET Core's built-in `RequireRole()` expects
  — enforced via an explicit `RequireAssertion` policy, at both the Gateway (for a clean GraphQL
  error) and RecipeService (the real boundary).

## API design

- **External** — mostly GraphQL (HotChocolate), the web-client's primary entry point, plus one
  plain REST endpoint:
  - Queries: `recipe(id)`, `recipes`, `ingredients`, `units`, `ingredientCategories`,
    `pantryItems`, `recipeMatches(ingredientIds)`, `confirmedStoreOffer(ingredientName, lat, lng)`,
    `nearbyStoresGeneral(lat, lng)`. A recipe's `tips`/`pairing` and each step's `imageUrl` ride
    along on `recipe(id)`.
  - Mutations: `upsertPantryItem`, `removePantryItem`, and admin-only `createRecipe`,
    `updateRecipe`, `deleteRecipe`, `createIngredient`, `updateIngredient`, `deleteIngredient`
    (the recipe mutations also carry `imageUrl`, `tips`, and `pairing`).
  - `POST /api/images/upload` — plain REST, not GraphQL; see
    [Image hosting](#image-hosting-pasted-url--cloudinary-upload--upload-only) for why.
  - None of the pantry or recipe-authoring operations take a caller/user id argument — identity
    always comes from the forwarded bearer token, never from a client-supplied value (see
    [Authentication and authorization](#authentication-and-authorization-for-real-users) below).
- **Internal** — plain REST over `HttpClient` between Gateway and each backend service
  (RecipeService additionally exposes `POST /api/images`, the real upload/validation endpoint
  Gateway's REST passthrough forwards to). gRPC was the original plan; see
  [Internal service calls](#internal-service-calls-grpc-planned--rest).
- **Events** — QStash webhook delivery, e.g. `ingredient.missing`; see
  [Event bus](#event-bus-kafka--qstash) below.

## Data model

### Relational core (Postgres, split by service ownership)

- `users(id, email, name, auth0_sub, created_at)` — RecipeService
- `recipes(id, author_id FK, title, description, servings, prep_time_min, cook_time_min, image_url, tips, pairing, created_at)` — RecipeService. `tips` is a native Postgres `TEXT[]`, `NOT NULL DEFAULT ARRAY[]::text[]`; `pairing` and `image_url` are nullable `TEXT`/`VARCHAR(2048)`.
- `recipe_steps(id, recipe_id FK, step_number, instruction, timer_seconds, image_url)` — RecipeService. `image_url` is nullable `VARCHAR(2048)`.
- `ingredients(id, name, category, default_unit)` — RecipeService (shared catalog; `category` and `default_unit` are validated server-side against curated enums, not free text; the admin catalog UI shows a usage count per ingredient and the delete endpoint blocks — 409, not a raw constraint error — when that count is non-zero)
- `recipe_ingredients(id, recipe_id FK, ingredient_id FK, quantity, unit, optional)` — RecipeService (`unit` is independent of the ingredient's `default_unit` — a recipe can call for an ingredient in a different unit than its catalog default, e.g. butter by weight in one recipe and by tablespoon in another)
- `pantry_items(id, user_id FK, ingredient_id FK, updated_at)` — PantryService (presence-only: has the ingredient or doesn't, no quantity/unit/expiry tracking)
- `stores(id, place_id, name, address, lat, lng)` — SourcingService (cached from Google Places)
- `ingredient_prices(id, ingredient_id FK, store_id FK, price, currency, observed_at)` — SourcingService

The substitution graph that used to live here (Neo4j, owned by SubstitutionService) was removed;
see [Considered / removed](#considered--removed) below.

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

### Authentication and authorization for real users

Nothing in the platform validated a caller's identity before this. Once real users were in the
picture, an audit of the existing code turned up several places where that mattered more than
"someday":

- **No authentication existed anywhere.** *Fixed:* all three backend services now validate Auth0
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
- **The `ingredient.missing` event carries a `UserId` that nothing uses.** The consumer is still
  a logging stub (see [System architecture](#system-architecture) above), so there's no per-user
  scoping to get wrong yet — but it will matter once it does real work.

Two of the items above (pantry access and recipe authorship) were genuine authorization
vulnerabilities in a live sense: an authenticated user could act as a different user, not just a
config gap. Both are called out specifically in the README's technical highlights.

Once auth existed, a second pass added role-gated writes: recipe and ingredient
create/update/delete now require an `admin` role claim, enforced at both the Gateway and
RecipeService (see [System architecture](#system-architecture) above). Image upload is gated the
same way, at both RecipeService's `/api/images` and Gateway's REST passthrough in front of it.

Every other test for an `AdminOnly` endpoint in this codebase constructs the controller directly
and calls the action method, which exercises the business logic but never the ASP.NET Core
authorization middleware itself — a non-admin-rejection test for any of them didn't exist. The
image-upload endpoint's test suite adds one that does: a `WebApplicationFactory`-based
integration test with a fake authentication handler standing in for Auth0, asserting the real
pipeline returns 403 without the admin claim and 200 with it.

### Kroger environment: Certification → Production

Kroger's integration originally targeted only the Certification (sandbox) environment.
`KrogerOptions` now has a `Kroger:Environment` switch (`Certification`/`Production`), each with
its own base URL and credentials — local dev defaults to Certification
(`api-ce.kroger.com`), and the production deployment sets `Kroger__Environment=Production` plus
production credentials as environment variables. Verified end-to-end against the real
`api.kroger.com` Production API: token refresh, location lookup, and product search all
succeeded with real store and price data returned.

### Sourcing: routing by capability, not by provider name

SourcingService runs two different flows against the same provider list — a confirmed,
per-ingredient price lookup (Kroger) and a general, ingredient-agnostic "nearby stores" locator
(Google Places) — and needs to route each incoming request to the right subset of providers.
That routing is driven by `IStoreProvider.IsIngredientSpecific`, a property every provider
declares about itself, not a check for the literal string `"Kroger"`. Adding a second
per-ingredient provider later means implementing the interface and setting the flag, not editing
an `if` statement that enumerates provider names.

The two flows also have deliberately different fallback behavior. The confirmed flow never falls
back to simulated data — an empty result means "not found," and a guess dressed up as a real
price would be worse than nothing. The general locator flow does fall back to a mock provider,
but only when every real provider returns nothing; the response carries a flag distinguishing the
two, and the web-client only ever presents a *confirmed* Kroger result as a real price, never the
general locator's fallback data as if it were a known in-stock item at a known price.

Two smaller decisions came out of actually wiring this into the recipe page. Kroger's API
returns no product-page URL for a result (confirmed by inspecting a real captured response —
only image asset URLs exist), so confirmed product names render as plain text; only the store
name links out, to Google Maps, same as the rest of the app's store links. And since both the
per-ingredient "Find it at Kroger" buttons and the general "Browse other nearby stores" button on
the same page need the browser's location, a shared `useGeolocation` hook resolves it once per
page view and reuses the result (or the in-flight request) for every caller, instead of prompting
for permission once per click.

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

### Recipe content: tips, pairing, and per-step images

Recipes gained three new fields: a short list of tips, an optional pairing suggestion, and an
image per step (alongside the recipe's existing hero `image_url`). All of it is optional, and the
detail page degrades deliberately rather than showing a gap: a step with no image renders as a
plain text card, not an empty box or a broken-image icon; a recipe with no hero image falls back
to the same hatch-pattern placeholder the recipe list already uses for a missing thumbnail,
reused rather than duplicated; the tips and pairing sections simply don't render at all when
empty.

**Tips as a native array, not a join table.** `recipes.tips` is a Postgres `TEXT[]`, not a
separate `recipe_tips(id, recipe_id, text, position)` table. A tip has no identity of its own —
nothing references one by id, nothing needs to query "which recipes have this tip," and the form
always edits and saves the entire list at once, the same full-replace semantics the form already
uses for steps and ingredients. A join table buys referential structure for data that doesn't
have any; an array column holds an ordered list directly, with no join and no extra table to
keep in sync. Server-side limits (10 tips max, 300 characters per tip, 500 for the pairing note)
are enforced in application code, not database constraints — they exist to stop unreasonably
large input, not to express a real data invariant.

**A real bug, caught by a real user, not by the test suite.** The migration that added `tips`
shipped it as a bare nullable column (`ALTER TABLE recipes ADD COLUMN tips TEXT[]`), while the
EF model declared the mapped property as a non-nullable `List<string>`. Every existing row's
`tips` was `NULL`. The repository tests all passed, because every one of them creates a recipe
through the EF model, which always supplies a non-null list — none of them exercised a row that
already existed before the column did. The gap only showed up against the live database, as
"Column 'tips' is null" when loading the recipe list. The fix had three parts: backfill existing
`NULL` rows to `'{}'`, add `DEFAULT ARRAY[]::text[]` and `NOT NULL` to the column so no future row
can repeat the problem, and a new repository test that inserts a row via raw SQL with the `tips`
column omitted — deliberately bypassing the EF model that every other test goes through — to
prove a row created by anything other than this application still loads correctly. The general
lesson carried forward: this project has no EF Core migrations, so a raw SQL migration file
(`init-db/*.sql`) and the EF model that describes the same table are two independently-maintained
descriptions of the same schema, kept in sync by hand rather than generated from one source of
truth — and a passing test suite that only ever writes data through the application can't catch
the two falling out of sync, since every row it ever creates necessarily matches the model it
was created with.

### Image hosting: pasted URL → Cloudinary upload → upload-only

Recipe images went through three stages. First, `image_url` fields accepted any `https://` URL,
validated only for scheme and length — someone else's hosting, pasted in by hand. Then real
upload shipped: an admin picks a file, RecipeService validates and pushes it to Cloudinary, and
the resulting URL is what gets stored — the URL-entry field stayed as a fallback alongside it.
Finally, URL entry was removed entirely; the hero and step image fields are upload-only, read-only
otherwise (a thumbnail and a label, never the raw URL).

**Validation is server-side and checks actual bytes, not metadata.** RecipeService verifies file
type from the first bytes of the upload (JPEG/PNG/WebP signatures), not the filename or the
client-declared `Content-Type` — both of those can be set to anything by the caller; the magic
bytes can't. A 5 MB size limit is enforced the same way, server-side, regardless of what the
client already did. The web-client downscales a selected image client-side first (long edge to
about 1600px, re-encoded as JPEG) purely so a multi-megabyte phone photo doesn't need to be
rejected and re-picked — it's a convenience, not a security boundary; the server never trusts
that a file arrived already downscaled.

**Gateway exposes a plain REST endpoint (`POST /api/images/upload`), not a GraphQL mutation.**
HotChocolate supports file upload via the GraphQL multipart-request spec, which would keep
Gateway's GraphQL-only external surface literally true. But every other GraphQL resolver in this
codebase is a thin translation layer forwarding to a backend's REST API over `HttpClient` — a
GraphQL upload resolver would still end up doing that same REST forward to RecipeService
underneath, so the GraphQL layer would add a new `Upload` scalar and (on the client) a multipart
exchange urql doesn't ship by default, for no behavioral difference from exposing the REST
forward directly. The REST endpoint is authorized with the same "clean error at the Gateway,
real enforcement downstream" pattern as every admin-only GraphQL mutation.

**A real .NET 8 behavior, found by running the endpoint, not by reading the docs.** The first
version of the Gateway endpoint 500'd on every request with "contains anti-forgery metadata, but
a middleware was not found." .NET 8 attaches antiforgery metadata to any endpoint that binds
`IFormFile`/form data — regardless of whether `AddAntiforgery()` is registered anywhere in the
app, which Gateway's `Program.cs` never does. The fix is `.DisableAntiforgery()` on the route,
with a comment explaining why it's safe: antiforgery protects against a forged request riding an
authenticated *cookie* session, and this endpoint is bearer-token authenticated — the same
forwarded `Authorization` header every other Gateway call already uses — so there's no cookie
session for a forged request to ride in the first place.

**Removing an image clears the field, and nothing else.** The "Remove image" button only clears
the recipe's `image_url` (or a step's) on the next save — it does not call Cloudinary to delete
the asset, deliberately: the removal doesn't take effect until the form is saved, and an
immediate delete would destroy the file even if the admin then cancels instead of saving. The
known cost of that choice: a removed or replaced image's old file stays in Cloudinary
indefinitely. Nothing in this codebase cleans it up — see [Current status](#current-status).

**Optional restriction to Cloudinary's own cloud, off by default.** `ImageUrlValidator` can
additionally require that a URL start with `https://res.cloudinary.com/{the configured cloud
name}/` — rejecting both an unrelated host and a *different* Cloudinary account's URL
specifically, not just anything that contains `cloudinary.com`. This is a
`CloudinaryOptions.RestrictImageUrlsToOwnCloud` flag, **off by default**, so turning it on is a
deliberate choice made after checking whether any already-stored recipe has an image from a
different host (a recipe that does would fail validation on its next edit, not on read, until its
image is replaced).

### Servings scaling: client-side and deliberately simple

The servings stepper on the recipe detail page scales every ingredient quantity by a plain ratio
(`adjustedServings / recipe.servings`) computed and applied entirely in the web-client — no
backend call, no AI, no per-ingredient scaling rules. That's a deliberate scope limit, not an
oversight, and it has real consequences the UI doesn't hide: seasoning, leavening (baking soda,
yeast), and similar quantities don't actually scale linearly in real cooking, and this does it
anyway, the same as every other ingredient. Step instructions and cook/prep times are never
adjusted at all — "simmer for 20 minutes" still reads 20 minutes at any serving count. A more
correct version would need per-ingredient scaling behavior and is explicitly not planned; this is
documented as a known limitation rather than fixed because it's a reasonable tradeoff for a
recipe app's actual use case, not a bug.

### Production scope: what ships to Render

Local dev and demo usage keep the full distributed architecture. Production, deployed on Render,
is intentionally simplified:

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

## Considered / removed

Decisions that shipped, were verified working, and were later taken back out entirely — kept
here rather than deleted from history, same spirit as the rest of this doc.

### Substitution graph (Neo4j) — removed

Ranked ingredient-substitution lookups ("what can replace butter for baking") shipped as their
own service, `SubstitutionService`, exposed through the Gateway as a nested
`RecipeIngredient.substitutions` resolver and surfaced in the web-client as a "Try instead"
suggestion next to any missing ingredient.

The data layer went through a deliberate migration before removal. It shipped first as a plain
Postgres table (`ingredient_id, substitute_id, ratio, context`) — "start simple, migrate when
it's outgrown" rather than reaching for a graph database on day one — then was retired in favor
of a Neo4j-backed graph once the model needed things a join table can't express cleanly:

- **Directionality/asymmetry** — applesauce can replace butter in a muffin recipe; butter can't
  replace applesauce in a smoothie. A symmetric join table can't represent this without
  duplicating rows and tracking direction by convention.
- **Context lives on the edge** — the same ingredient pair can have different ratios depending on
  cooking method (frying vs. baking), which belongs naturally on a weighted, labeled relationship
  rather than as extra table columns multiplying the row count.
- **Transitive lookups are cheap** — "what else substitutes for something that substitutes for
  X" is a 2-hop graph traversal, not a recursive CTE.

The resulting schema:

- Nodes: `Ingredient(name, category)`
- Relationship: `SUBSTITUTES_FOR(ratio, contexts[], confidence)` — directional and weighted

Example query — "what can I use instead of butter for baking, ranked by confidence":

```cypher
MATCH (b:Ingredient {name: "Butter"})-[s:SUBSTITUTES_FOR]->(alt:Ingredient)
WHERE "baking" IN s.contexts
RETURN alt.name, s.ratio, s.confidence
ORDER BY s.confidence DESC
```

**Why it was removed, in October 2026:** no real substitution data was ever populated into the
graph — it was architecturally real (built, deployed, verified end-to-end, Gateway's client
degraded gracefully when it was unreachable) but functionally empty. The hosted Neo4j AuraDB
instance was free-tier and expired from inactivity; rather than provision a replacement for a
feature with no actual data behind it, the feature was removed outright: `SubstitutionService`,
Gateway's client/resolver/DataLoader for it, and the web-client's "Try instead" UI. The
`ingredient.missing` QStash event still exists and is still delivered to SourcingService
unaffected — only the SubstitutionService side of that fan-out is gone.

If ranked substitutions come back, this is still the right shape for the data — the reasoning
above didn't change, only the fact that nothing was populating it.

## Current status

**Working today:** recipe browsing/creation/editing (admin-gated) with a hero image, per-step
images, tips, and a pairing note; admin image upload to Cloudinary with server-side file-type and
size validation; drag-to-reorder recipe steps, with each step's image moving with it; pantry
tracking; ranked recipe matching against pantry contents; real Kroger pricing and Google Places
store lookups run in parallel with timeout/fallback handling; client-side servings scaling; Auth0
login with role-based write access and JIT user provisioning; and full OpenTelemetry
tracing/Prometheus/Grafana metrics in local dev. Production is deployed on Render against
Kroger's real Production API.

**Not built, and not pretending otherwise:**

- gRPC between services (see [above](#internal-service-calls-grpc-planned--rest))
- Temporal workflow orchestration (see [above](#workflow-orchestration-temporal--evaluated-not-built))
- A deliberate on-call/incident-review simulation — alerting rules, fault injection, and a
  written postmortem were planned as a way to manufacture on-call experience solo, but weren't
  executed
- Cleanup for orphaned Cloudinary assets — a removed or replaced image's old file is never
  deleted (see [Image hosting](#image-hosting-pasted-url--cloudinary-upload--upload-only) above)
- Recipe drafts, cook history, and a shopping list generated from missing ingredients — none of
  these exist; "missing ingredients" is computed on demand per recipe, not accumulated anywhere
- Multi-language recipe content — titles, steps, tips, and pairing notes are single-language,
  plain-text fields with no localization model
- Any downstream action on the `ingredient.missing` event — the consumer currently just logs it
- Unit conversion in the missing-ingredients diff — it compares quantities directly and assumes
  the pantry item's unit already matches the recipe's
- Production observability — see [Production scope](#production-scope-what-ships-to-render) above
  for the honest tradeoff this represents
- Ranked ingredient substitutions — built, verified, then removed; see
  [Considered / removed](#considered--removed) above
