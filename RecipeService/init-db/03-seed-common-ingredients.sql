-- One-time manual seed for the already-provisioned Neon database, run once by hand (this
-- project has no EF Core migrations -- 01-schema.sql only applies to a fresh database).
-- Superseded for any future fresh install by pairing 01-schema.sql with this script.
--
-- Seeds a broad catalog of common cooking ingredients across all 15 categories from
-- RecipeService.Domain.IngredientCategories, so there's something realistic to browse/pantry
-- against beyond the handful of rows created ad hoc during earlier development.
-- Category and unit codes below are copied verbatim from IngredientCategories.cs and
-- MeasurementUnits.cs -- do not hand-edit a code here without checking it's still valid there.
--
-- ON CONFLICT (name) DO NOTHING makes this safe to re-run and safe against unknown existing
-- rows (name is UNIQUE; a handful of rows already exist per 02-require-ingredient-category.sql's
-- comments, e.g. "Cooking Oil", "White Sugar", "Vinegar" -- none of those exact names are reused
-- below, but the clause guards against any other overlap too).

-- Vegetables (25)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Yellow Onion',     'vegetables', 'whole'),
    ('Red Onion',        'vegetables', 'whole'),
    ('Garlic',           'vegetables', 'clove'),
    ('Roma Tomato',      'vegetables', 'whole'),
    ('Cherry Tomatoes',  'vegetables', 'g'),
    ('Carrot',           'vegetables', 'whole'),
    ('Celery',           'vegetables', 'whole'),
    ('Bell Pepper',      'vegetables', 'whole'),
    ('Jalapeno',         'vegetables', 'whole'),
    ('Broccoli',         'vegetables', 'g'),
    ('Cauliflower',      'vegetables', 'whole'),
    ('Spinach',          'vegetables', 'g'),
    ('Kale',             'vegetables', 'g'),
    ('Romaine Lettuce',  'vegetables', 'whole'),
    ('Cabbage',          'vegetables', 'whole'),
    ('Zucchini',         'vegetables', 'whole'),
    ('Yellow Squash',    'vegetables', 'whole'),
    ('Cucumber',         'vegetables', 'whole'),
    ('Potato',           'vegetables', 'whole'),
    ('Sweet Potato',     'vegetables', 'whole'),
    ('Mushroom',         'vegetables', 'g'),
    ('Green Beans',      'vegetables', 'g'),
    ('Asparagus',        'vegetables', 'g'),
    ('Corn on the Cob',  'vegetables', 'whole'),
    ('Scallion',         'vegetables', 'whole')
ON CONFLICT (name) DO NOTHING;

-- Fruit (22)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Apple',            'fruit', 'whole'),
    ('Banana',           'fruit', 'whole'),
    ('Orange',           'fruit', 'whole'),
    ('Lemon',            'fruit', 'whole'),
    ('Lime',             'fruit', 'whole'),
    ('Grapefruit',       'fruit', 'whole'),
    ('Strawberries',     'fruit', 'g'),
    ('Blueberries',      'fruit', 'g'),
    ('Raspberries',      'fruit', 'g'),
    ('Blackberries',     'fruit', 'g'),
    ('Grapes',           'fruit', 'g'),
    ('Watermelon',       'fruit', 'whole'),
    ('Cantaloupe',       'fruit', 'whole'),
    ('Pineapple',        'fruit', 'whole'),
    ('Mango',            'fruit', 'whole'),
    ('Avocado',          'fruit', 'whole'),
    ('Peach',            'fruit', 'whole'),
    ('Pear',             'fruit', 'whole'),
    ('Plum',             'fruit', 'whole'),
    ('Cherries',         'fruit', 'g'),
    ('Kiwi',             'fruit', 'whole'),
    ('Pomegranate',      'fruit', 'whole')
ON CONFLICT (name) DO NOTHING;

-- Meat & Poultry (16)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Chicken Breast',    'meat_poultry', 'lb'),
    ('Chicken Thighs',    'meat_poultry', 'lb'),
    ('Whole Chicken',     'meat_poultry', 'whole'),
    ('Ground Chicken',    'meat_poultry', 'lb'),
    ('Ground Beef',       'meat_poultry', 'lb'),
    ('Beef Chuck Roast',  'meat_poultry', 'lb'),
    ('Ribeye Steak',      'meat_poultry', 'lb'),
    ('Beef Sirloin',      'meat_poultry', 'lb'),
    ('Ground Pork',       'meat_poultry', 'lb'),
    ('Pork Chops',        'meat_poultry', 'lb'),
    ('Pork Tenderloin',   'meat_poultry', 'lb'),
    ('Bacon',             'meat_poultry', 'oz'),
    ('Ham',               'meat_poultry', 'lb'),
    ('Ground Turkey',     'meat_poultry', 'lb'),
    ('Turkey Breast',     'meat_poultry', 'lb'),
    ('Lamb Chops',        'meat_poultry', 'lb')
ON CONFLICT (name) DO NOTHING;

-- Seafood (15)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Shrimp',            'seafood', 'lb'),
    ('Salmon Fillet',     'seafood', 'lb'),
    ('Tilapia Fillet',    'seafood', 'lb'),
    ('Cod Fillet',        'seafood', 'lb'),
    ('Tuna Steak',        'seafood', 'lb'),
    ('Halibut Fillet',    'seafood', 'lb'),
    ('Scallops',          'seafood', 'lb'),
    ('Mussels',           'seafood', 'lb'),
    ('Clams',             'seafood', 'lb'),
    ('Crab Meat',         'seafood', 'lb'),
    ('Lobster Tail',      'seafood', 'whole'),
    ('Calamari',          'seafood', 'lb'),
    ('Sea Bass Fillet',   'seafood', 'lb'),
    ('Catfish Fillet',    'seafood', 'lb'),
    ('Trout Fillet',      'seafood', 'lb')
ON CONFLICT (name) DO NOTHING;

-- Dairy & Eggs (16)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Whole Milk',        'dairy_eggs', 'cup'),
    ('Skim Milk',         'dairy_eggs', 'cup'),
    ('Heavy Cream',       'dairy_eggs', 'cup'),
    ('Half and Half',     'dairy_eggs', 'cup'),
    ('Unsalted Butter',   'dairy_eggs', 'tbsp'),
    ('Salted Butter',     'dairy_eggs', 'tbsp'),
    ('Large Eggs',        'dairy_eggs', 'whole'),
    ('Egg Whites',        'dairy_eggs', 'cup'),
    ('Cheddar Cheese',    'dairy_eggs', 'g'),
    ('Mozzarella Cheese', 'dairy_eggs', 'g'),
    ('Parmesan Cheese',   'dairy_eggs', 'g'),
    ('Cream Cheese',      'dairy_eggs', 'oz'),
    ('Sour Cream',        'dairy_eggs', 'cup'),
    ('Plain Yogurt',      'dairy_eggs', 'cup'),
    ('Greek Yogurt',      'dairy_eggs', 'cup'),
    ('Buttermilk',        'dairy_eggs', 'cup')
ON CONFLICT (name) DO NOTHING;

-- Grains & Pasta (18)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Jasmine Rice',        'grains_pasta', 'cup'),
    ('Basmati Rice',        'grains_pasta', 'cup'),
    ('Brown Rice',          'grains_pasta', 'cup'),
    ('White Rice',          'grains_pasta', 'cup'),
    ('Quinoa',              'grains_pasta', 'cup'),
    ('Rolled Oats',         'grains_pasta', 'cup'),
    ('Spaghetti',           'grains_pasta', 'oz'),
    ('Penne Pasta',         'grains_pasta', 'oz'),
    ('Egg Noodles',         'grains_pasta', 'oz'),
    ('Macaroni',            'grains_pasta', 'oz'),
    ('Couscous',            'grains_pasta', 'cup'),
    ('Barley',              'grains_pasta', 'cup'),
    ('White Bread',         'grains_pasta', 'whole'),
    ('Whole Wheat Bread',   'grains_pasta', 'whole'),
    ('Tortilla',            'grains_pasta', 'whole'),
    ('Bread Crumbs',        'grains_pasta', 'cup'),
    ('Panko Breadcrumbs',   'grains_pasta', 'cup'),
    ('Cornmeal',            'grains_pasta', 'cup')
ON CONFLICT (name) DO NOTHING;

-- Legumes & Beans (12)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Black Beans',        'legumes_beans', 'cup'),
    ('Kidney Beans',       'legumes_beans', 'cup'),
    ('Pinto Beans',        'legumes_beans', 'cup'),
    ('Chickpeas',          'legumes_beans', 'cup'),
    ('Cannellini Beans',   'legumes_beans', 'cup'),
    ('Navy Beans',         'legumes_beans', 'cup'),
    ('Lentils',            'legumes_beans', 'cup'),
    ('Split Peas',         'legumes_beans', 'cup'),
    ('Black-Eyed Peas',    'legumes_beans', 'cup'),
    ('Edamame',            'legumes_beans', 'cup'),
    ('Refried Beans',      'legumes_beans', 'cup'),
    ('Lima Beans',         'legumes_beans', 'cup')
ON CONFLICT (name) DO NOTHING;

-- Herbs (15)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Basil',       'herbs', 'tbsp'),
    ('Cilantro',    'herbs', 'tbsp'),
    ('Parsley',     'herbs', 'tbsp'),
    ('Mint',        'herbs', 'tbsp'),
    ('Dill',        'herbs', 'tbsp'),
    ('Chives',      'herbs', 'tbsp'),
    ('Rosemary',    'herbs', 'tsp'),
    ('Thyme',       'herbs', 'tsp'),
    ('Oregano',     'herbs', 'tsp'),
    ('Sage',        'herbs', 'tsp'),
    ('Tarragon',    'herbs', 'tsp'),
    ('Marjoram',    'herbs', 'tsp'),
    ('Bay Leaf',    'herbs', 'whole'),
    ('Lemongrass',  'herbs', 'whole'),
    ('Chervil',     'herbs', 'tbsp')
ON CONFLICT (name) DO NOTHING;

-- Spices & Seasonings (22)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Salt',                 'spices_seasonings', 'tsp'),
    ('Black Pepper',         'spices_seasonings', 'tsp'),
    ('Paprika',              'spices_seasonings', 'tsp'),
    ('Smoked Paprika',       'spices_seasonings', 'tsp'),
    ('Ground Cumin',         'spices_seasonings', 'tsp'),
    ('Chili Powder',         'spices_seasonings', 'tsp'),
    ('Ground Cinnamon',      'spices_seasonings', 'tsp'),
    ('Ground Turmeric',      'spices_seasonings', 'tsp'),
    ('Cayenne Pepper',       'spices_seasonings', 'tsp'),
    ('Garlic Powder',        'spices_seasonings', 'tsp'),
    ('Onion Powder',         'spices_seasonings', 'tsp'),
    ('Ground Ginger',        'spices_seasonings', 'tsp'),
    ('Ground Nutmeg',        'spices_seasonings', 'tsp'),
    ('Ground Coriander',     'spices_seasonings', 'tsp'),
    ('Curry Powder',         'spices_seasonings', 'tsp'),
    ('Red Pepper Flakes',    'spices_seasonings', 'tsp'),
    ('Italian Seasoning',    'spices_seasonings', 'tsp'),
    ('Ground Allspice',      'spices_seasonings', 'tsp'),
    ('Whole Cloves',         'spices_seasonings', 'tsp'),
    ('Fennel Seed',          'spices_seasonings', 'tsp'),
    ('Mustard Seed',         'spices_seasonings', 'tsp'),
    ('Chinese Five Spice',   'spices_seasonings', 'tsp')
ON CONFLICT (name) DO NOTHING;

-- Baking & Flour (18)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('All-Purpose Flour',      'baking_flour', 'cup'),
    ('Whole Wheat Flour',      'baking_flour', 'cup'),
    ('Bread Flour',            'baking_flour', 'cup'),
    ('Cake Flour',             'baking_flour', 'cup'),
    ('Granulated Sugar',       'baking_flour', 'cup'),
    ('Brown Sugar',            'baking_flour', 'cup'),
    ('Powdered Sugar',         'baking_flour', 'cup'),
    ('Baking Powder',          'baking_flour', 'tsp'),
    ('Baking Soda',            'baking_flour', 'tsp'),
    ('Active Dry Yeast',       'baking_flour', 'tsp'),
    ('Cocoa Powder',           'baking_flour', 'tbsp'),
    ('Vanilla Extract',        'baking_flour', 'tsp'),
    ('Cornstarch',             'baking_flour', 'tbsp'),
    ('Chocolate Chips',        'baking_flour', 'cup'),
    ('Semi-Sweet Chocolate',   'baking_flour', 'oz'),
    ('Almond Flour',           'baking_flour', 'cup'),
    ('Graham Cracker Crumbs',  'baking_flour', 'cup'),
    ('Molasses',               'baking_flour', 'tbsp')
ON CONFLICT (name) DO NOTHING;

-- Oils & Fats (11)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Olive Oil',              'oils_fats', 'tbsp'),
    ('Extra Virgin Olive Oil', 'oils_fats', 'tbsp'),
    ('Vegetable Oil',          'oils_fats', 'tbsp'),
    ('Canola Oil',             'oils_fats', 'tbsp'),
    ('Sesame Oil',             'oils_fats', 'tsp'),
    ('Coconut Oil',            'oils_fats', 'tbsp'),
    ('Avocado Oil',            'oils_fats', 'tbsp'),
    ('Peanut Oil',             'oils_fats', 'tbsp'),
    ('Ghee',                   'oils_fats', 'tbsp'),
    ('Shortening',             'oils_fats', 'cup'),
    ('Cooking Spray',          'oils_fats', 'tsp')
ON CONFLICT (name) DO NOTHING;

-- Sauces & Condiments (20)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Soy Sauce',              'sauces_condiments', 'tbsp'),
    ('Ketchup',                'sauces_condiments', 'tbsp'),
    ('Yellow Mustard',         'sauces_condiments', 'tbsp'),
    ('Dijon Mustard',          'sauces_condiments', 'tbsp'),
    ('Mayonnaise',             'sauces_condiments', 'tbsp'),
    ('Hot Sauce',              'sauces_condiments', 'tsp'),
    ('Worcestershire Sauce',   'sauces_condiments', 'tbsp'),
    ('BBQ Sauce',              'sauces_condiments', 'tbsp'),
    ('Salsa',                  'sauces_condiments', 'cup'),
    ('Hoisin Sauce',           'sauces_condiments', 'tbsp'),
    ('Oyster Sauce',           'sauces_condiments', 'tbsp'),
    ('Fish Sauce',             'sauces_condiments', 'tsp'),
    ('Sriracha',               'sauces_condiments', 'tbsp'),
    ('Ranch Dressing',         'sauces_condiments', 'tbsp'),
    ('Balsamic Vinegar',       'sauces_condiments', 'tbsp'),
    ('Apple Cider Vinegar',    'sauces_condiments', 'tbsp'),
    ('Rice Vinegar',           'sauces_condiments', 'tbsp'),
    ('Tahini',                 'sauces_condiments', 'tbsp'),
    ('Pesto',                  'sauces_condiments', 'tbsp'),
    ('Teriyaki Sauce',         'sauces_condiments', 'tbsp')
ON CONFLICT (name) DO NOTHING;

-- Canned & Jarred (14)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Canned Diced Tomatoes', 'canned_jarred', 'cup'),
    ('Tomato Paste',          'canned_jarred', 'tbsp'),
    ('Tomato Sauce',          'canned_jarred', 'cup'),
    ('Canned Corn',           'canned_jarred', 'cup'),
    ('Canned Tuna',           'canned_jarred', 'oz'),
    ('Canned Coconut Milk',   'canned_jarred', 'cup'),
    ('Roasted Red Peppers',   'canned_jarred', 'cup'),
    ('Green Chilies',         'canned_jarred', 'oz'),
    ('Pickles',               'canned_jarred', 'whole'),
    ('Capers',                'canned_jarred', 'tbsp'),
    ('Kalamata Olives',       'canned_jarred', 'g'),
    ('Artichoke Hearts',      'canned_jarred', 'cup'),
    ('Peanut Butter',         'canned_jarred', 'tbsp'),
    ('Applesauce',            'canned_jarred', 'cup')
ON CONFLICT (name) DO NOTHING;

-- Nuts & Seeds (15)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Almonds',           'nuts_seeds', 'g'),
    ('Walnuts',           'nuts_seeds', 'g'),
    ('Cashews',           'nuts_seeds', 'g'),
    ('Peanuts',           'nuts_seeds', 'g'),
    ('Pecans',            'nuts_seeds', 'g'),
    ('Pistachios',        'nuts_seeds', 'g'),
    ('Macadamia Nuts',    'nuts_seeds', 'g'),
    ('Hazelnuts',         'nuts_seeds', 'g'),
    ('Pine Nuts',         'nuts_seeds', 'g'),
    ('Sunflower Seeds',   'nuts_seeds', 'g'),
    ('Pumpkin Seeds',     'nuts_seeds', 'g'),
    ('Chia Seeds',        'nuts_seeds', 'tbsp'),
    ('Flax Seeds',        'nuts_seeds', 'tbsp'),
    ('Sesame Seeds',      'nuts_seeds', 'tbsp'),
    ('Poppy Seeds',       'nuts_seeds', 'tbsp')
ON CONFLICT (name) DO NOTHING;

-- Other (6)
INSERT INTO ingredients (name, category, default_unit) VALUES
    ('Nutritional Yeast',  'other', 'tbsp'),
    ('Gelatin',            'other', 'tsp'),
    ('Food Coloring',      'other', 'tsp'),
    ('Liquid Smoke',       'other', 'tsp'),
    ('Cream of Tartar',    'other', 'tsp'),
    ('Xanthan Gum',        'other', 'tsp')
ON CONFLICT (name) DO NOTHING;
