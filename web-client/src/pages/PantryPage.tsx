import { useState } from 'react';
import { useMutation, useQuery } from 'urql';
import {
  INGREDIENTS_QUERY,
  PANTRY_ITEMS_QUERY,
  REMOVE_PANTRY_ITEM_MUTATION,
  UPSERT_PANTRY_ITEM_MUTATION,
} from '../graphql/queries';
import type { Ingredient, PantryItem } from '../graphql/types';

export function PantryPage() {
  const [{ data: pantryData, fetching: pantryFetching, error: pantryError }, refetchPantryItems] = useQuery<{
    pantryItems: PantryItem[];
  }>({ query: PANTRY_ITEMS_QUERY });

  const [{ data: ingredientsData, fetching: ingredientsFetching }] = useQuery<{ ingredients: Ingredient[] }>({
    query: INGREDIENTS_QUERY,
  });

  const [, upsertPantryItem] = useMutation(UPSERT_PANTRY_ITEM_MUTATION);
  const [, removePantryItem] = useMutation(REMOVE_PANTRY_ITEM_MUTATION);

  const [pendingIds, setPendingIds] = useState<Set<number>>(new Set());
  const [error, setError] = useState<string | null>(null);

  const ingredients = ingredientsData?.ingredients ?? [];
  const pantryIngredientIds = new Set(pantryData?.pantryItems.map((item) => item.ingredientId) ?? []);

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

  return (
    <div>
      <h1>Pantry</h1>
      <p>Check off the ingredients you currently have on hand.</p>

      {error && <p className="error-message">{error}</p>}
      {(pantryFetching || ingredientsFetching) && <p>Loading pantry...</p>}
      {pantryError && <p className="error-message">Failed to load pantry: {pantryError.message}</p>}
      {!pantryFetching && !ingredientsFetching && ingredients.length === 0 && <p>No ingredients yet.</p>}

      {ingredients.map((ingredient) => {
        const inPantry = pantryIngredientIds.has(ingredient.id);
        return (
          <label key={ingredient.id} className="pantry-checklist-item">
            <input
              type="checkbox"
              checked={inPantry}
              disabled={pendingIds.has(ingredient.id)}
              onChange={() => handleToggle(ingredient.id, inPantry)}
            />
            {ingredient.name}
          </label>
        );
      })}
    </div>
  );
}
