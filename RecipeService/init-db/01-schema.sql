-- Recipe service schema. Owned exclusively by recipe-service; no other
-- service is permitted to connect to this database (see docs/design.md:
-- "no shared database").

CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE ingredients (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    category VARCHAR(100),
    default_unit VARCHAR(50) NOT NULL
);

CREATE TABLE recipes (
    id BIGSERIAL PRIMARY KEY,
    author_id BIGINT NOT NULL REFERENCES users (id),
    title VARCHAR(255) NOT NULL,
    description TEXT,
    servings INTEGER,
    prep_time_min INTEGER,
    cook_time_min INTEGER,
    image_url VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_recipes_author_id ON recipes (author_id);

CREATE TABLE recipe_steps (
    id BIGSERIAL PRIMARY KEY,
    recipe_id BIGINT NOT NULL REFERENCES recipes (id) ON DELETE CASCADE,
    step_number INTEGER NOT NULL,
    instruction TEXT NOT NULL,
    timer_seconds INTEGER,
    UNIQUE (recipe_id, step_number)
);

CREATE TABLE recipe_ingredients (
    id BIGSERIAL PRIMARY KEY,
    recipe_id BIGINT NOT NULL REFERENCES recipes (id) ON DELETE CASCADE,
    ingredient_id BIGINT NOT NULL REFERENCES ingredients (id),
    quantity NUMERIC(10, 2) NOT NULL,
    unit VARCHAR(50) NOT NULL,
    optional BOOLEAN NOT NULL DEFAULT false
);

CREATE INDEX idx_recipe_ingredients_recipe_id ON recipe_ingredients (recipe_id);
CREATE INDEX idx_recipe_ingredients_ingredient_id ON recipe_ingredients (ingredient_id);

-- Phase 1 "open decision" from docs/design.md: substitution logic starts as
-- a plain relational lookup table here, and migrates to the Neo4j graph
-- (directional, weighted, context-aware edges) owned by a standalone
-- Substitution service in Phase 2.
CREATE TABLE ingredient_substitutions (
    id BIGSERIAL PRIMARY KEY,
    ingredient_id BIGINT NOT NULL REFERENCES ingredients (id),
    substitute_id BIGINT NOT NULL REFERENCES ingredients (id),
    ratio NUMERIC(10, 4) NOT NULL DEFAULT 1.0,
    context VARCHAR(100),
    CHECK (ingredient_id <> substitute_id)
);

CREATE INDEX idx_ingredient_substitutions_ingredient_id ON ingredient_substitutions (ingredient_id);
