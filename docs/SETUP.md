# Local setup

Account-by-account walkthrough for running Larder locally. This is one-time setup; day-to-day
running is just `.\start-all.ps1` — see the [README](../README.md) for that and for which
config key each service expects each of these values under.

For *why* the system is shaped the way it is, see [`docs/design.md`](design.md) instead — this
file is purely about getting a working local environment.

1. **Auth0** — create a tenant, then:
   - an **SPA application** for `web-client` (copy its domain/client id into
     `web-client/.env.local`, based on `web-client/.env.example`)
   - an **API** with identifier `https://recipemate.api` (this is the audience every backend
     service validates against)
   - a **Post-Login Action**, wired into the Login flow, that adds `email`/`name` claims to the
     token (Auth0 doesn't include them by default) and, for at least your own test user, an
     `admin` entry in a `https://recipemate.api/roles` array claim — without this, recipe/
     ingredient create/edit/delete will 403
2. **Postgres** (Neon or local) — a database reachable from RecipeService and PantryService; run
   each service's `init-db/*.sql` against it to create the schema.
3. **Redis** — any reachable instance, for SourcingService's result cache.
4. **Kroger Developer Portal** — register an app to get Certification-environment credentials
   (Production credentials are a separate application-review process, only needed for a real
   deployment).
5. **Google Cloud** — enable the Places API and generate an API key.
6. **QStash** — either run the local dev server (`npx @upstash/qstash-cli dev`, or let
   `start-all.ps1` do it) for local testing, or create an Upstash QStash instance for a real
   deployment.

Wire the resulting values into each service via `dotnet user-secrets set` or environment
variables — the README's "Configure secrets" section lists which keys each service expects.
