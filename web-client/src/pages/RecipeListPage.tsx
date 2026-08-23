import { useMemo, useState } from 'react';
import { useQuery } from 'urql';
import { Link } from 'react-router-dom';
import { useIsAdmin } from '../auth/useIsAdmin';
import { buttonVariants } from '../components/ui/button';
import { PANTRY_ITEMS_QUERY, RECIPES_QUERY, RECIPE_MATCHES_QUERY } from '../graphql/queries';
import type { PantryItem, RecipeMatch, RecipeSummary } from '../graphql/types';

export function RecipeListPage() {
  const isAdmin = useIsAdmin();
  const [cookableOnly, setCookableOnly] = useState(false);
  const [under30, setUnder30] = useState(false);

  const [{ data, fetching, error }] = useQuery<{ recipes: RecipeSummary[] }>({
    query: RECIPES_QUERY,
  });

  // Paused unless the "Cookable tonight" filter is on, so browsing the list doesn't cost an
  // extra round trip when nobody's asked for it.
  const [{ data: pantryData, fetching: pantryFetching }] = useQuery<{ pantryItems: PantryItem[] }>({
    query: PANTRY_ITEMS_QUERY,
    pause: !cookableOnly,
  });

  const ingredientIds = useMemo(
    () => [...new Set(pantryData?.pantryItems.map((item) => item.ingredientId) ?? [])].sort((a, b) => a - b),
    [pantryData],
  );

  const [{ data: matchesData, fetching: matchesFetching }] = useQuery<
    { recipeMatches: RecipeMatch[] },
    { ingredientIds: number[] }
  >({
    query: RECIPE_MATCHES_QUERY,
    variables: { ingredientIds },
    pause: !cookableOnly || ingredientIds.length === 0,
  });

  if (fetching) return <p>Loading recipes...</p>;
  if (error) return <p className="error-message">Failed to load recipes: {error.message}</p>;

  const recipes = data?.recipes ?? [];

  const cookableIds = new Set(
    (matchesData?.recipeMatches ?? [])
      .filter((m) => m.missingIngredients.length === 0)
      .map((m) => m.recipe.id),
  );

  let filtered = recipes;
  if (under30) {
    // Excludes recipes with neither time set -- there's nothing to base "under 30" on.
    filtered = filtered.filter((r) => {
      if (r.prepTimeMin == null && r.cookTimeMin == null) return false;
      return (r.prepTimeMin ?? 0) + (r.cookTimeMin ?? 0) <= 30;
    });
  }
  if (cookableOnly) {
    filtered = filtered.filter((r) => cookableIds.has(r.id));
  }

  const cookableEmptyPantry = cookableOnly && !pantryFetching && ingredientIds.length === 0;
  const cookableLoading = cookableOnly && (pantryFetching || (ingredientIds.length > 0 && matchesFetching));

  return (
    <div>
      <div className="page-header">
        <h1>Recipes</h1>
        {isAdmin && (
          <Link to="/recipes/new" className={buttonVariants({ variant: 'default' })}>
            Add Recipe
          </Link>
        )}
      </div>
      <div className="content-panel">
        <div className="panel-toolbar">
          <p className="stat-line">{recipes.length} RECIPES</p>
          <div className="filter-bar">
            <button
              type="button"
              className={`filter-toggle${cookableOnly ? ' active' : ''}`}
              aria-pressed={cookableOnly}
              onClick={() => setCookableOnly((v) => !v)}
            >
              Cookable tonight
            </button>
            <button
              type="button"
              className={`filter-toggle${under30 ? ' active' : ''}`}
              aria-pressed={under30}
              onClick={() => setUnder30((v) => !v)}
            >
              Under 30 min
            </button>
          </div>
        </div>

        {recipes.length === 0 && <p>No recipes yet.</p>}
        {recipes.length > 0 && cookableEmptyPantry && (
          <p>Add ingredients on the Pantry page to see what's cookable tonight.</p>
        )}
        {recipes.length > 0 && cookableLoading && <p>Finding recipes...</p>}
        {recipes.length > 0 && !cookableEmptyPantry && !cookableLoading && filtered.length === 0 && (
          <p>No recipes match these filters.</p>
        )}

        <div className="card-grid">
          {filtered.map((recipe) => (
            <Link key={recipe.id} to={`/recipes/${recipe.id}`} className="recipe-card">
              <div
                className="recipe-thumb"
                style={recipe.imageUrl ? { backgroundImage: `url(${recipe.imageUrl})` } : undefined}
              />
              <h2>{recipe.title}</h2>
              {recipe.description && <p className="recipe-note">{recipe.description}</p>}
              <div className="recipe-meta">
                {recipe.prepTimeMin != null && <span>PREP {recipe.prepTimeMin}m</span>}
                {recipe.cookTimeMin != null && <span>COOK {recipe.cookTimeMin}m</span>}
                {recipe.servings != null && <span>SERVES {recipe.servings}</span>}
              </div>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
