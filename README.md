# Recipe Platform — Phase 1

MVP slice of the architecture described in [`docs/design.md`](docs/design.md): two independently
deployable services, each owning its own Postgres database, talking to each other over HTTP —
no shared schema, no cross-service SQL joins.

- **RecipeService** (`:5081`) — recipes, steps, ingredient catalog. Owns `recipe_db`.
- **PantryService** (`:5082`) — per-user pantry inventory. Owns `pantry_db`. Resolves ingredient
  names and recipe contents by calling RecipeService's REST API (see
  `PantryService/PantryService/Client/RecipeServiceClient.cs`), never by joining across databases.

## Prerequisites

- Docker Desktop (for `docker compose up`)
- .NET 8 SDK — only needed for local (non-Docker) dev/test runs

> This scaffold was built without a local .NET 8 SDK or Docker install available, so it hasn't
> been compiled here. Run `dotnet build` / `docker compose build` first thing to catch anything
> that needs adjusting for your machine's exact SDK/package versions.

## Run everything

```bash
docker compose up --build
```

- RecipeService: http://localhost:5081/swagger
- PantryService: http://localhost:5082/swagger
- Postgres (recipe_db): localhost:5433
- Postgres (pantry_db): localhost:5434

Schemas are created automatically on first container start via the SQL scripts in
`RecipeService/init-db/` and `PantryService/init-db/` (mounted into each Postgres container's
`docker-entrypoint-initdb.d/`).

## Try the end-to-end flow

```bash
# 1. Create a user and an ingredient in RecipeService
curl -X POST localhost:5081/api/users -H 'Content-Type: application/json' \
  -d '{"email":"chef@example.com","name":"Chef"}'
curl -X POST localhost:5081/api/ingredients -H 'Content-Type: application/json' \
  -d '{"name":"Flour","category":"Baking","defaultUnit":"g"}'

# 2. Create a recipe that needs 500g of it
curl -X POST localhost:5081/api/recipes -H 'Content-Type: application/json' \
  -d '{"authorId":1,"title":"Bread","steps":[{"stepNumber":1,"instruction":"Mix and bake"}],"ingredients":[{"ingredientId":1,"quantity":500,"unit":"g","optional":false}]}'

# 3. Add only 100g to the user's pantry, via PantryService
curl -X PUT localhost:5082/api/pantry/users/1/items -H 'Content-Type: application/json' \
  -d '{"ingredientId":1,"quantity":100,"unit":"g"}'

# 4. Ask PantryService what's missing for the recipe — it calls RecipeService under the hood
curl localhost:5082/api/pantry/users/1/recipes/1/missing-ingredients
```

That last endpoint is the seed of the Phase 2 `ingredient.missing` event flow described in the
design doc — right now it's a synchronous diff; Phase 2 turns it into an async Kafka-triggered
workflow.

## Run locally without Docker

Point the connection strings at `localhost:5433` / `localhost:5434` (already the defaults in
`appsettings.json`) and `RecipeService:BaseUrl` at wherever RecipeService is listening, then:

```bash
dotnet run --project RecipeService/RecipeService
dotnet run --project PantryService/PantryService
```

You'll still need the two Postgres containers running (`docker compose up recipe-db pantry-db`).

## Tests

```bash
dotnet test
```

Controller/service tests run against mocks with no external dependencies. The repository tests
(`*RepositoryTests.cs`) use Testcontainers to spin up a throwaway Postgres container per run, so
they need a reachable Docker daemon.

## Known Phase 1 simplifications

- The missing-ingredients diff compares quantities directly with no unit conversion — it assumes
  the pantry item's unit matches the recipe's.
- No auth/authz yet; `userId`/`authorId` are passed as plain path/body values.
