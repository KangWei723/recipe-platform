-- One-time manual migration for the already-provisioned Neon database, run once by hand
-- (this project has no EF Core migrations -- 01-schema.sql only applies to a fresh database).
-- Superseded for any future fresh install by 01-schema.sql, which already declares
-- `category VARCHAR(100) NOT NULL`.
--
-- Backfills the only 4 ingredient rows that existed at the time category became required
-- (checked live via `SELECT id, name, category FROM ingredients ORDER BY id;` -- all 4 had
-- category = NULL, no free-text values to reconcile), then enforces NOT NULL going forward.

UPDATE ingredients SET category = 'oils_fats' WHERE id = 6;         -- Cooking Oil
UPDATE ingredients SET category = 'baking_flour' WHERE id = 7;      -- White Sugar
UPDATE ingredients SET category = 'sauces_condiments' WHERE id = 8; -- Vinegar
UPDATE ingredients SET category = 'meat_poultry' WHERE id = 9;      -- Drumstick

ALTER TABLE ingredients ALTER COLUMN category SET NOT NULL;
