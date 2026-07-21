-- Pantry service schema. Owned exclusively by pantry-service.
--
-- user_id and ingredient_id are intentionally plain BIGINTs with no foreign
-- key constraint: those entities live in recipe-service's database, and
-- per docs/design.md ("no shared database... it calls Recipe's API rather
-- than joining across schemas"), cross-service references are resolved over
-- HTTP (see PantryService/Client/RecipeServiceClient.cs), never via SQL join.

CREATE TABLE pantry_items (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    ingredient_id BIGINT NOT NULL,
    quantity NUMERIC(10, 2) NOT NULL,
    unit VARCHAR(50) NOT NULL,
    expiry_date DATE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, ingredient_id)
);

CREATE INDEX idx_pantry_items_user_id ON pantry_items (user_id);
