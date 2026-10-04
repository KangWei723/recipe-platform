-- One-time manual migration for the already-provisioned Neon database, run once by hand
-- (this project has no EF Core migrations -- 01-schema.sql only applies to a fresh database).
-- Superseded for any future fresh install by 01-schema.sql, which already declares these columns.
--
-- Phase 1 image/tips/pairing support: per-step images, a recipe tips list, and a pairing note.
-- recipe_steps.image_url and recipes.pairing are nullable with no DEFAULT -- safe metadata-only
-- additions regardless of existing row count. recipes.image_url is widened to match (signed CDN
-- URLs routinely exceed the original 500-char limit), also metadata-only.
--
-- recipes.tips is NOT nullable (RecipeStep.Tips is a non-nullable List<string> in the EF model),
-- so it needs the full add-nullable -> backfill -> enforce-NOT-NULL sequence, same shape as
-- 02-require-ingredient-category.sql: ADD COLUMN with no DEFAULT leaves every existing row NULL,
-- the UPDATE backfills those to an empty array, then DEFAULT/NOT NULL are set going forward. This
-- step was missed on the first pass of this migration (shipped as a bare nullable `tips TEXT[]`)
-- and caused "Column 'tips' is null" errors reading pre-migration rows back through EF -- fixed
-- here; the equivalent three statements were already run by hand against the live Neon database.
--
-- IMPORTANT: this project has no EF Core migrations, so RecipeDbContext.OnModelCreating (the
-- model the Testcontainers repository tests build their schema from via EnsureCreatedAsync) and
-- 01-schema.sql (what a fresh install/this migration apply to a real Postgres) are two
-- independently-maintained descriptions of the same schema. Keep them in sync by hand -- check
-- column types/lengths/nullability match in both places whenever either one changes.

ALTER TABLE recipe_steps ADD COLUMN image_url VARCHAR(2048);
ALTER TABLE recipes ALTER COLUMN image_url TYPE VARCHAR(2048);
ALTER TABLE recipes ADD COLUMN pairing TEXT;

ALTER TABLE recipes ADD COLUMN tips TEXT[];
UPDATE recipes SET tips = ARRAY[]::text[] WHERE tips IS NULL;
ALTER TABLE recipes ALTER COLUMN tips SET DEFAULT ARRAY[]::text[];
ALTER TABLE recipes ALTER COLUMN tips SET NOT NULL;
