import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery } from 'urql';
import { Input } from '../components/ui/input';
import {
  INGREDIENTS_QUERY,
  PANTRY_ITEMS_QUERY,
  RECIPE_MATCHES_QUERY,
  REMOVE_PANTRY_ITEM_MUTATION,
  UPSERT_PANTRY_ITEM_MUTATION,
} from '../graphql/queries';
import { useIngredientCategories } from '../graphql/useIngredientCategories';
import type { Ingredient, PantryItem, RecipeMatch } from '../graphql/types';

export function PantryPage() {
  const [{ data: pantryData, fetching: pantryFetching, error: pantryError }, refetchPantryItems] = useQuery<{
    pantryItems: PantryItem[];
  }>({ query: PANTRY_ITEMS_QUERY });

  const [{ data: ingredientsData, fetching: ingredientsFetching }] = useQuery<{ ingredients: Ingredient[] }>({
    query: INGREDIENTS_QUERY,
  });

  const { categories } = useIngredientCategories();

  const [, upsertPantryItem] = useMutation(UPSERT_PANTRY_ITEM_MUTATION);
  const [, removePantryItem] = useMutation(REMOVE_PANTRY_ITEM_MUTATION);

  const [pendingIds, setPendingIds] = useState<Set<number>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  const ingredients = ingredientsData?.ingredients ?? [];
  const pantryItems = pantryData?.pantryItems ?? [];
  const pantryIngredientIds = new Set(pantryItems.map((item) => item.ingredientId));

  const ingredientIds = useMemo(
    () => [...pantryIngredientIds].sort((a, b) => a - b),
    // pantryItems is the actual dependency; pantryIngredientIds is derived from it each render
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pantryItems],
  );

  const [{ data: matchesData, fetching: matchesFetching, error: matchesError }] = useQuery<
    { recipeMatches: RecipeMatch[] },
    { ingredientIds: number[] }
  >({
    query: RECIPE_MATCHES_QUERY,
    variables: { ingredientIds },
    pause: ingredientIds.length === 0,
  });

  async function handleToggle(ingredientId: number, currentlyInPantry: boolean) {
    setError(null);
    setPendingIds((ids) => new Set(ids).add(ingredientId));

    const result = currentlyInPantry
      ? await removePantryItem({ ingredientId })
      : await upsertPantryItem({ ingredientId });

    setPendingIds((ids) => {
      const next = new Set(ids);
      next.delete(ingredientId);
      return next;
    });

    if (result.error) {
      setError(result.error.message);
      return;
    }

    refetchPantryItems({ requestPolicy: 'network-only' });
  }

  const filteredIngredients = ingredients.filter((ingredient) =>
    ingredient.name.toLowerCase().includes(search.trim().toLowerCase()),
  );

  const groupedIngredients = categories
    .map((category) => ({
      category,
      items: filteredIngredients.filter((ingredient) => ingredient.category === category.code),
    }))
    .filter((group) => group.items.length > 0);

  const sortedPantryItems = [...pantryItems].sort((a, b) => a.ingredientName.localeCompare(b.ingredientName));
  const matches = matchesData?.recipeMatches ?? [];

  return (
    <div>
      <h1>Pantry</h1>

      {error && <p className="error-message">{error}</p>}
      {pantryError && <p className="error-message">Failed to load pantry: {pantryError.message}</p>}

      <div className="content-panel">
        <div className="pantry-top-grid">
          <div className="ingredient-picker">
            <h2>What's in the kitchen</h2>
            <p className="panel-subtitle">Tick what you've got. We'll work out what you can cook right now.</p>
            <Input
              type="search"
              placeholder="Search ingredients..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search ingredients"
            />

            {(pantryFetching || ingredientsFetching) && <p>Loading ingredients...</p>}
            {!pantryFetching && !ingredientsFetching && filteredIngredients.length === 0 && (
              <p>No ingredients match "{search}".</p>
            )}

            {groupedIngredients.map((group) => (
              <div key={group.category.code} className="ingredient-picker-group">
                <h3 className="ingredient-picker-group-label">{group.category.label}</h3>
                <div className="ingredient-pill-row">
                  {group.items.map((ingredient) => {
                    const inPantry = pantryIngredientIds.has(ingredient.id);
                    return (
                      <button
                        key={ingredient.id}
                        type="button"
                        className={`ingredient-pill${inPantry ? ' on' : ''}`}
                        disabled={pendingIds.has(ingredient.id)}
                        onClick={() => handleToggle(ingredient.id, inPantry)}
                        aria-pressed={inPantry}
                      >
                        {ingredient.name}
                      </button>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>

          <div className="panel pantry-shelf-panel">
            <span className="panel-label">On the shelf &middot; {sortedPantryItems.length}</span>
            {sortedPantryItems.length === 0 ? (
              <p>Nothing selected yet -- tick ingredients on the left.</p>
            ) : (
              <div className="chip-strip">
                {sortedPantryItems.map((item) => (
                  <button
                    key={item.ingredientId}
                    type="button"
                    className="chip"
                    disabled={pendingIds.has(item.ingredientId)}
                    onClick={() => handleToggle(item.ingredientId, true)}
                    aria-label={`Remove ${item.ingredientName}`}
                  >
                    {item.ingredientName}
                    <span className="chip-remove" aria-hidden="true">
                      &times;
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="section-divider" />

        <div>
          <h2>Recipes you can make</h2>
          {ingredientIds.length === 0 && <p>Add ingredients above to see what you can cook.</p>}
          {matchesFetching && <p>Finding recipes...</p>}
          {matchesError && <p className="error-message">Failed to load recipe matches: {matchesError.message}</p>}
          {!matchesFetching && ingredientIds.length > 0 && matches.length === 0 && <p>No recipes yet.</p>}

          <div className="card-grid">
            {matches.map((match) => {
              const fullMatch = match.missingIngredients.length === 0;
              return (
                <Link key={match.recipe.id} to={`/recipes/${match.recipe.id}`} className="recipe-card match-card">
                  <div className="match-card-header">
                    <h2>{match.recipe.title}</h2>
                    <span className={`match-tag ${fullMatch ? 'match-tag--full' : 'match-tag--partial'}`}>
                      {fullMatch ? 'All here' : `Short ${match.missingIngredients.length}`}
                    </span>
                  </div>
                  <div>
                    <span className="match-gauge" aria-hidden="true">
                      {Array.from({ length: match.requiredIngredientCount }).map((_, i) => (
                        <span
                          key={i}
                          className={`match-gauge-tick${i < match.matchedIngredientCount ? ' filled' : ''}`}
                        />
                      ))}
                    </span>
                    <span className="match-ratio">
                      {match.matchedIngredientCount}/{match.requiredIngredientCount}
                    </span>
                  </div>
                  {!fullMatch && (
                    <ul className="match-missing-list">
                      {match.missingIngredients.map((missing) => (
                        <li key={missing.ingredientId} className="ingredient-row">
                          <span className="ingredient-dot" aria-hidden="true" />
                          <span className="ingredient-name">{missing.ingredientName}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </Link>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
