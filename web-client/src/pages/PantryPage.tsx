import { useState, type FormEvent } from 'react';
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

  const [ingredientId, setIngredientId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const ingredients = ingredientsData?.ingredients ?? [];

  function handleIngredientChange(newIngredientId: string) {
    setIngredientId(newIngredientId);
    const selected = ingredients.find((i) => String(i.id) === newIngredientId);
    if (selected) {
      setUnit(selected.defaultUnit);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    if (!ingredientId || !quantity || !unit) {
      setFormError('Ingredient, quantity, and unit are required.');
      return;
    }

    const result = await upsertPantryItem({
      ingredientId: Number(ingredientId),
      quantity: Number(quantity),
      unit,
      expiryDate: expiryDate || null,
    });

    if (result.error) {
      setFormError(result.error.message);
      return;
    }

    setIngredientId('');
    setQuantity('');
    setUnit('');
    setExpiryDate('');
    refetchPantryItems({ requestPolicy: 'network-only' });
  }

  async function handleRemove(itemId: number) {
    const result = await removePantryItem({ itemId });
    if (!result.error) {
      refetchPantryItems({ requestPolicy: 'network-only' });
    }
  }

  return (
    <div>
      <h1>Pantry</h1>

      <form className="pantry-form" onSubmit={handleSubmit}>
        <label>
          Ingredient
          <select
            value={ingredientId}
            onChange={(e) => handleIngredientChange(e.target.value)}
            disabled={ingredientsFetching}
          >
            <option value="">Select...</option>
            {ingredients.map((i) => (
              <option key={i.id} value={i.id}>
                {i.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Quantity
          <input
            type="number"
            min="0"
            step="any"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
          />
        </label>
        <label>
          Unit
          <input type="text" value={unit} onChange={(e) => setUnit(e.target.value)} />
        </label>
        <label>
          Expiry date (optional)
          <input type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} />
        </label>
        <button type="submit">Add / Update</button>
      </form>
      {formError && <p className="error-message">{formError}</p>}

      {pantryFetching && <p>Loading pantry...</p>}
      {pantryError && <p className="error-message">Failed to load pantry: {pantryError.message}</p>}
      {pantryData?.pantryItems.length === 0 && <p>Your pantry is empty.</p>}
      {pantryData?.pantryItems.map((item) => (
        <div key={item.id} className="pantry-item-row">
          <span>
            {item.quantity} {item.unit} {item.ingredientName}
            {item.expiryDate ? ` (expires ${item.expiryDate})` : ''}
          </span>
          <button type="button" onClick={() => handleRemove(item.id)}>
            Remove
          </button>
        </div>
      ))}
    </div>
  );
}
