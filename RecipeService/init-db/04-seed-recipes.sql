-- One-off manual seed for the already-provisioned Neon database, run once by hand
-- (this project has no EF Core migrations -- 01-schema.sql only applies to a fresh database).
--
-- Loads 18 recipes extracted from docs/recipe-seed-data.docx: title, description, servings,
-- prep/cook time, ingredients (matched by exact name against 03-seed-common-ingredients.sql --
-- every ingredient name in the source document matched an existing row, so nothing here creates
-- new ingredients), and numbered steps. Ingredient quantities/units are taken verbatim from the
-- document rather than normalized to each ingredient's default_unit -- default_unit is only the
-- Add Recipe form's pre-fill suggestion (RecipeService.Domain.MeasurementUnits), never enforced
-- against recipe_ingredients.unit, and several of the document's choices (e.g. cup for shredded
-- cabbage, lb for asparagus) are simply more natural for that recipe than the catalog default.
--
-- All recipes are authored by auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb' (resolved to a numeric users.id
-- via subquery below rather than a hardcoded id, since that id wasn't looked up directly against
-- Neon). Each recipe is skipped (not duplicated) if a recipe with the same title already exists,
-- so this script is safe to re-run.
--
-- Every ingredient_id below is also resolved via a per-row subquery rather than a join, so that
-- if an ingredient name doesn't match an existing row, the insert fails loudly on the
-- recipe_ingredients.ingredient_id NOT NULL constraint instead of silently dropping that row.

-- Classic Scrambled Eggs with Chives
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Classic Scrambled Eggs with Chives',
    'Soft, creamy scrambled eggs finished with fresh chives — a quick, reliable breakfast staple.',
    2,
    5,
    5
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Classic Scrambled Eggs with Chives')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Crack the eggs into a bowl, add the milk, and whisk until fully combined and slightly frothy.', NULL),
    (2, 'Melt the butter in a nonstick skillet over medium-low heat, swirling to coat the pan.', 60),
    (3, 'Pour in the egg mixture and let it sit undisturbed for 20 seconds before gently folding with a spatula.', 20),
    (4, 'Continue folding gently every 20-30 seconds until the eggs are just set but still glossy, about 3 minutes total.', 180),
    (5, 'Season with salt and pepper, sprinkle with chives, and serve immediately.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Large Eggs'), 4, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Whole Milk'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salted Butter'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Chives'), 1, 'tbsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.25, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.25, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Buttermilk Pancakes
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Buttermilk Pancakes',
    'Light, fluffy pancakes with a tender crumb, thanks to buttermilk and a careful hand with the batter.',
    4,
    10,
    15
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Buttermilk Pancakes')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Whisk the flour, sugar, baking powder, baking soda, and salt together in a large bowl.', NULL),
    (2, 'In a separate bowl, whisk the buttermilk, eggs, melted butter, and vanilla extract until smooth.', NULL),
    (3, 'Pour the wet ingredients into the dry ingredients and stir just until combined; a few lumps are fine.', NULL),
    (4, 'Heat a griddle or nonstick pan over medium heat and ladle about 1/4 cup of batter per pancake.', NULL),
    (5, 'Cook until bubbles form on the surface and the edges look set, about 2-3 minutes, then flip and cook 1-2 minutes more.', 150),
    (6, 'Repeat with the remaining batter and serve warm.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'All-Purpose Flour'), 2, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Granulated Sugar'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Baking Powder'), 2, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Baking Soda'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Buttermilk'), 1.75, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Large Eggs'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Unsalted Butter'), 3, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Vanilla Extract'), 1, 'tsp', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Overnight Blueberry Oats
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Overnight Blueberry Oats',
    'No-cook, make-ahead oats that turn creamy overnight in the fridge — ready to grab on a busy morning.',
    2,
    10,
    0
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Overnight Blueberry Oats')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Combine the oats, Greek yogurt, milk, chia seeds, and vanilla extract in a jar or airtight container.', NULL),
    (2, 'Stir well to make sure the oats are fully submerged in the liquid.', NULL),
    (3, 'Cover and refrigerate for at least 6 hours, or overnight.', 21600),
    (4, 'Stir again before serving and top with the blueberries.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Rolled Oats'), 1, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Greek Yogurt'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Whole Milk'), 1, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Blueberries'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Chia Seeds'), 1, 'tbsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Vanilla Extract'), 0.5, 'tsp', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Avocado Toast with Cherry Tomatoes
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Avocado Toast with Cherry Tomatoes',
    'A bright, simple open-faced toast — creamy avocado, juicy cherry tomatoes, and a hit of lemon and chili.',
    2,
    10,
    3
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Avocado Toast with Cherry Tomatoes')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Toast the bread slices until golden and crisp, about 2-3 minutes.', 180),
    (2, 'Halve the cherry tomatoes and set aside.', NULL),
    (3, 'Mash the avocado in a bowl with a squeeze of lemon juice and a pinch of salt.', NULL),
    (4, 'Spread the mashed avocado over the toast, top with cherry tomatoes, and drizzle with olive oil.', NULL),
    (5, 'Finish with a pinch of red pepper flakes, if using.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Whole Wheat Bread'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Avocado'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Cherry Tomatoes'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Lemon'), 0.5, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Red Pepper Flakes'), 0.25, 'tsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.25, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil'), 1, 'tsp', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Classic Beef Chili
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Classic Beef Chili',
    'A rich, slow-simmered ground beef chili loaded with kidney beans and warm spices.',
    6,
    15,
    45
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Classic Beef Chili')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Heat the olive oil in a large pot over medium heat and sauté the onion, garlic, and bell pepper until softened, about 5 minutes.', 300),
    (2, 'Add the ground beef and cook, breaking it up with a spoon, until browned, about 6-8 minutes.', 480),
    (3, 'Stir in the diced tomatoes, tomato paste, kidney beans, chili powder, cumin, and smoked paprika.', NULL),
    (4, 'Bring to a simmer, then reduce the heat to low and cook uncovered, stirring occasionally, for 30 minutes.', 1800),
    (5, 'Season with salt and black pepper to taste before serving.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Ground Beef'), 1, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Yellow Onion'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 3, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Bell Pepper'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Canned Diced Tomatoes'), 2, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Tomato Paste'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Kidney Beans'), 1.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Chili Powder'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Ground Cumin'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Smoked Paprika'), 1, 'tsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Olive Oil'), 1, 'tbsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Spaghetti Aglio e Olio
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Spaghetti Aglio e Olio',
    'The classic Roman pantry pasta — garlic slowly bloomed in olive oil, brightened with chili flakes and parsley.',
    4,
    5,
    15
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Spaghetti Aglio e Olio')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Bring a large pot of salted water to a boil and cook the spaghetti until al dente, about 9-10 minutes.', 600),
    (2, 'While the pasta cooks, thinly slice the garlic.', NULL),
    (3, 'Warm the olive oil in a large skillet over low heat, add the garlic and red pepper flakes, and cook gently until the garlic is fragrant and golden, about 3 minutes.', 180),
    (4, 'Reserve 1/2 cup of pasta water, then drain the spaghetti and add it to the skillet.', NULL),
    (5, 'Toss the pasta with the garlic oil, adding a splash of pasta water to loosen the sauce as needed.', NULL),
    (6, 'Stir in the parsley, top with Parmesan, and serve immediately.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Spaghetti'), 12, 'oz', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 6, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Red Pepper Flakes'), 1, 'tsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Parsley'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Parmesan Cheese'), 30, 'g', true),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Chicken and Broccoli Stir-Fry
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Chicken and Broccoli Stir-Fry',
    'A fast weeknight stir-fry with tender chicken, crisp broccoli, and a glossy soy-sesame sauce.',
    4,
    15,
    15
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Chicken and Broccoli Stir-Fry')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Slice the chicken breast into thin strips and toss with the cornstarch and 1 tablespoon of the soy sauce.', NULL),
    (2, 'Heat the vegetable oil in a wok or large skillet over high heat and stir-fry the chicken until browned and cooked through, about 5 minutes; remove and set aside.', 300),
    (3, 'Add the broccoli to the pan with a splash of water and stir-fry until crisp-tender, about 4 minutes.', 240),
    (4, 'Add the garlic and ginger and cook for 30 seconds until fragrant.', 30),
    (5, 'Return the chicken to the pan, add the remaining soy sauce and the sesame oil, and toss to coat.', NULL),
    (6, 'Garnish with sliced scallion and serve over rice.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Chicken Breast'), 1, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Broccoli'), 300, 'g', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 2, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Ground Ginger'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Soy Sauce'), 3, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Sesame Oil'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Cornstarch'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Vegetable Oil'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Scallion'), 2, 'whole', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Shrimp Tacos with Lime Crema
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Shrimp Tacos with Lime Crema',
    'Chili-spiced shrimp, crisp cabbage, and a tangy lime crema wrapped in warm tortillas.',
    4,
    15,
    8
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Shrimp Tacos with Lime Crema')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Toss the shrimp with the olive oil, chili powder, garlic powder, and salt.', NULL),
    (2, 'Whisk the sour cream with the juice of one lime to make the crema; set aside.', NULL),
    (3, 'Thinly shred the cabbage.', NULL),
    (4, 'Heat a skillet over medium-high heat and cook the shrimp until pink and opaque, about 2-3 minutes per side.', 300),
    (5, 'Warm the tortillas, then fill with cabbage and shrimp.', NULL),
    (6, 'Drizzle with lime crema, sprinkle with cilantro, and serve with the remaining lime wedges.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Shrimp'), 1, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Tortilla'), 8, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Sour Cream'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Lime'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Cilantro'), 2, 'tbsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Cabbage'), 2, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Chili Powder'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic Powder'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Olive Oil'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.5, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Baked Salmon with Asparagus
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Baked Salmon with Asparagus',
    'A simple sheet-pan dinner — salmon and asparagus roasted together with lemon, olive oil, and garlic.',
    4,
    10,
    18
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Baked Salmon with Asparagus')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Preheat the oven to 400°F (200°C) and line a baking sheet with foil.', NULL),
    (2, 'Trim the woody ends off the asparagus and arrange it alongside the salmon on the baking sheet.', NULL),
    (3, 'Drizzle everything with olive oil, then season with garlic powder, salt, and black pepper.', NULL),
    (4, 'Slice half the lemon and tuck the slices under the salmon; squeeze the other half over the top.', NULL),
    (5, 'Bake until the salmon flakes easily and the asparagus is tender, 15-18 minutes.', 1080),
    (6, 'Garnish with fresh dill before serving.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Salmon Fillet'), 1.5, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Asparagus'), 1, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Lemon'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic Powder'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.75, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Dill'), 1, 'tbsp', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Creamy Tomato Basil Soup
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Creamy Tomato Basil Soup',
    'A smooth, comforting tomato soup finished with a splash of cream and fresh basil.',
    4,
    10,
    25
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Creamy Tomato Basil Soup')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Heat the olive oil in a pot over medium heat and sauté the onion until soft and translucent, about 5 minutes.', 300),
    (2, 'Add the garlic and cook for 30 seconds until fragrant.', 30),
    (3, 'Stir in the diced tomatoes and tomato paste, then bring to a simmer and cook for 15 minutes.', 900),
    (4, 'Purée the soup with an immersion blender until smooth.', NULL),
    (5, 'Stir in the heavy cream and basil, then season with salt and pepper.', NULL),
    (6, 'Simmer for a further 5 minutes before serving.', 300)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Canned Diced Tomatoes'), 3, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Tomato Paste'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Yellow Onion'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 2, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Olive Oil'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Heavy Cream'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Basil'), 0.25, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.5, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Roasted Chicken Thighs with Rosemary and Sweet Potato
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Roasted Chicken Thighs with Rosemary and Sweet Potato',
    'Crisp-skinned chicken thighs roasted on a single sheet pan with rosemary and sweet potato.',
    4,
    15,
    40
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Roasted Chicken Thighs with Rosemary and Sweet Potato')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Preheat the oven to 425°F (220°C).', NULL),
    (2, 'Cut the sweet potatoes into 1-inch chunks and toss them on a sheet pan with half the olive oil, salt, and pepper.', NULL),
    (3, 'Rub the chicken thighs with the remaining olive oil, minced garlic, rosemary, salt, and pepper.', NULL),
    (4, 'Nestle the chicken thighs among the sweet potatoes, skin-side up.', NULL),
    (5, 'Roast until the chicken is golden and cooked through and the sweet potatoes are tender, about 35-40 minutes.', 2400),
    (6, 'Rest for 5 minutes before serving.', 300)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Chicken Thighs'), 2, 'lb', false),
    ((SELECT id FROM ingredients WHERE name = 'Sweet Potato'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Rosemary'), 1, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 4, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil'), 3, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.5, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Black Bean and Corn Salad
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Black Bean and Corn Salad',
    'A crisp, no-cook salad of black beans and corn in a bright lime-cumin dressing.',
    4,
    15,
    0
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Black Bean and Corn Salad')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Drain and rinse the black beans and corn, then combine them in a large bowl.', NULL),
    (2, 'Dice the bell pepper and red onion and add them to the bowl.', NULL),
    (3, 'In a small bowl, whisk together the lime juice, olive oil, cumin, and salt.', NULL),
    (4, 'Pour the dressing over the salad and toss to combine.', NULL),
    (5, 'Chill for at least 15 minutes, then stir in the cilantro just before serving.', 900)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Black Beans'), 1.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Canned Corn'), 1, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Bell Pepper'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Red Onion'), 0.25, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Cilantro'), 2, 'tbsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Lime'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Ground Cumin'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.5, 'tsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Hearty Lentil Soup
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Hearty Lentil Soup',
    'A warming, vegetarian lentil soup built on a classic base of onion, carrot, and celery.',
    6,
    15,
    35
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Hearty Lentil Soup')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Rinse the lentils under cold water and set aside.', NULL),
    (2, 'Heat the olive oil in a large pot over medium heat and sauté the onion, carrot, and celery until softened, about 6 minutes.', 360),
    (3, 'Add the garlic, cumin, and turmeric and cook for 1 minute until fragrant.', 60),
    (4, 'Add the lentils and enough water to cover by about 2 inches; bring to a boil.', NULL),
    (5, 'Reduce the heat and simmer until the lentils are tender, about 25-30 minutes.', 1800),
    (6, 'Season with salt and pepper, and finish with a squeeze of lemon juice.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Lentils'), 1.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Carrot'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Celery'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Yellow Onion'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Garlic'), 2, 'clove', false),
    ((SELECT id FROM ingredients WHERE name = 'Ground Cumin'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Ground Turmeric'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Olive Oil'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Black Pepper'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Lemon'), 0.5, 'whole', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Classic Chocolate Chip Cookies
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Classic Chocolate Chip Cookies',
    'Chewy-centered, crisp-edged chocolate chip cookies — a reliable everyday batch.',
    24,
    15,
    11
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Classic Chocolate Chip Cookies')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Preheat the oven to 375°F (190°C) and line two baking sheets with parchment paper.', NULL),
    (2, 'Whisk together the flour, baking soda, and salt in a bowl and set aside.', NULL),
    (3, 'In a large bowl, cream the butter with the granulated and brown sugars until light and fluffy, about 3 minutes.', 180),
    (4, 'Beat in the eggs and vanilla extract.', NULL),
    (5, 'Gradually mix in the dry ingredients until just combined, then fold in the chocolate chips.', NULL),
    (6, 'Drop rounded tablespoons of dough onto the baking sheets, spaced 2 inches apart.', NULL),
    (7, 'Bake until the edges are golden but the centers still look slightly underdone, 9-11 minutes.', 660),
    (8, 'Cool on the baking sheet for 5 minutes before transferring to a wire rack.', 300)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'All-Purpose Flour'), 2.25, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Baking Soda'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Unsalted Butter'), 1, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Granulated Sugar'), 0.75, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Brown Sugar'), 0.75, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Large Eggs'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Vanilla Extract'), 2, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Chocolate Chips'), 2, 'cup', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Banana Bread
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Banana Bread',
    'A moist, classic banana bread with a tender crumb and an optional walnut crunch.',
    8,
    15,
    55
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Banana Bread')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Preheat the oven to 350°F (175°C) and grease a 9x5-inch loaf pan.', NULL),
    (2, 'Mash the bananas in a large bowl until mostly smooth.', NULL),
    (3, 'Melt the butter and stir it into the mashed banana along with the sugar, eggs, and vanilla extract.', NULL),
    (4, 'Sprinkle the baking soda and salt over the mixture and stir in.', NULL),
    (5, 'Add the flour and mix until just combined; fold in the walnuts if using.', NULL),
    (6, 'Pour the batter into the prepared pan and bake until a toothpick inserted in the center comes out clean, 55-60 minutes.', 3300),
    (7, 'Cool in the pan for 10 minutes, then turn out onto a wire rack to cool completely.', 600)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Banana'), 3, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'All-Purpose Flour'), 2, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Granulated Sugar'), 0.75, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Unsalted Butter'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Large Eggs'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Baking Soda'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Vanilla Extract'), 1, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Walnuts'), 0.5, 'cup', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Classic Guacamole
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Classic Guacamole',
    'A simple, chunky guacamole with lime, cilantro, and a bit of heat from jalapeño.',
    4,
    10,
    0
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Classic Guacamole')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Halve and pit the avocados, then scoop the flesh into a bowl.', NULL),
    (2, 'Mash the avocado to your desired consistency with a fork.', NULL),
    (3, 'Finely dice the red onion, jalapeño, and tomato, and add them to the bowl.', NULL),
    (4, 'Stir in the lime juice, chopped cilantro, and salt.', NULL),
    (5, 'Taste and adjust the seasoning, then serve immediately with tortilla chips.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Avocado'), 3, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Lime'), 1, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Red Onion'), 0.25, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Jalapeno'), 1, 'whole', true),
    ((SELECT id FROM ingredients WHERE name = 'Cilantro'), 2, 'tbsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Salt'), 0.5, 'tsp', false),
    ((SELECT id FROM ingredients WHERE name = 'Roma Tomato'), 1, 'whole', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Grilled Cheese Sandwich
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Grilled Cheese Sandwich',
    'A quick, golden, ultra-cheesy grilled cheese — a five-minute lunch or snack.',
    1,
    5,
    6
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Grilled Cheese Sandwich')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Butter one side of each slice of bread.', NULL),
    (2, 'Place one slice butter-side down in a skillet over medium heat and top with the cheddar cheese.', NULL),
    (3, 'Top with the second slice of bread, butter-side up.', NULL),
    (4, 'Cook until the bottom is golden brown, about 3 minutes.', 180),
    (5, 'Flip carefully and cook until the second side is golden and the cheese is melted, about 3 minutes.', 180)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'White Bread'), 2, 'whole', false),
    ((SELECT id FROM ingredients WHERE name = 'Cheddar Cheese'), 60, 'g', false),
    ((SELECT id FROM ingredients WHERE name = 'Salted Butter'), 1, 'tbsp', false)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;

-- Pesto Penne with Pine Nuts
WITH new_recipe AS (
  INSERT INTO recipes (author_id, title, description, servings, prep_time_min, cook_time_min)
  SELECT
    (SELECT id FROM users WHERE auth0_sub = 'auth0|6a8b0ff53e041b725d1de8bb'),
    'Pesto Penne with Pine Nuts',
    'A fast vegetarian pasta tossed in basil pesto with toasted pine nuts and cherry tomatoes.',
    4,
    5,
    12
  WHERE NOT EXISTS (SELECT 1 FROM recipes WHERE title = 'Pesto Penne with Pine Nuts')
  RETURNING id
),
new_steps AS (
  INSERT INTO recipe_steps (recipe_id, step_number, instruction, timer_seconds)
  SELECT new_recipe.id, s.step_number, s.instruction, s.timer_seconds
  FROM new_recipe
  CROSS JOIN (VALUES
    (1, 'Bring a large pot of salted water to a boil and cook the penne until al dente, about 11-12 minutes.', 720),
    (2, 'Reserve 1/2 cup of pasta water, then drain the pasta.', NULL),
    (3, 'Return the pasta to the pot and toss with the pesto, adding pasta water as needed to loosen the sauce.', NULL),
    (4, 'Halve the cherry tomatoes, if using, and fold them in.', NULL),
    (5, 'Top with Parmesan cheese and toasted pine nuts before serving.', NULL)
  ) AS s(step_number, instruction, timer_seconds)
  RETURNING 1
),
new_ingredients AS (
  INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, optional)
  SELECT new_recipe.id, x.ingredient_id, x.quantity, x.unit, x.optional
  FROM new_recipe
  CROSS JOIN (VALUES
    ((SELECT id FROM ingredients WHERE name = 'Penne Pasta'), 12, 'oz', false),
    ((SELECT id FROM ingredients WHERE name = 'Pesto'), 0.5, 'cup', false),
    ((SELECT id FROM ingredients WHERE name = 'Parmesan Cheese'), 30, 'g', false),
    ((SELECT id FROM ingredients WHERE name = 'Pine Nuts'), 2, 'tbsp', true),
    ((SELECT id FROM ingredients WHERE name = 'Cherry Tomatoes'), 1, 'cup', true)
  ) AS x(ingredient_id, quantity, unit, optional)
  RETURNING 1
)
SELECT 1;
