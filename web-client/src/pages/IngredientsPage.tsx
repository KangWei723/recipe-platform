import { useState, type FormEvent } from 'react';
import { useMutation, useQuery } from 'urql';
import { useIsAdmin } from '../auth/useIsAdmin';
import {
  CREATE_INGREDIENT_MUTATION,
  DELETE_INGREDIENT_MUTATION,
  INGREDIENTS_QUERY,
  UPDATE_INGREDIENT_MUTATION,
} from '../graphql/queries';
import type { Ingredient } from '../graphql/types';
import { formatMutationError } from '../utils/errors';

interface EditState {
  name: string;
  category: string;
  defaultUnit: string;
}

function toEditState(ingredient: Ingredient): EditState {
  return { name: ingredient.name, category: ingredient.category ?? '', defaultUnit: ingredient.defaultUnit };
}

export function IngredientsPage() {
  const isAdmin = useIsAdmin();

  const [{ data, fetching, error }, refetch] = useQuery<{ ingredients: Ingredient[] }>({
    query: INGREDIENTS_QUERY,
  });

  const [, createIngredient] = useMutation(CREATE_INGREDIENT_MUTATION);
  const [, updateIngredient] = useMutation(UPDATE_INGREDIENT_MUTATION);
  const [, deleteIngredient] = useMutation(DELETE_INGREDIENT_MUTATION);

  const [name, setName] = useState('');
  const [category, setCategory] = useState('');
  const [defaultUnit, setDefaultUnit] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editState, setEditState] = useState<EditState | null>(null);
  const [rowError, setRowError] = useState<{ id: number; message: string } | null>(null);

  if (!isAdmin) {
    return <p>Admin access required to manage ingredients.</p>;
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    if (!name.trim() || !defaultUnit.trim()) {
      setFormError('Name and default unit are required.');
      return;
    }

    setSubmitting(true);
    const result = await createIngredient({
      name: name.trim(),
      category: category.trim() || null,
      defaultUnit: defaultUnit.trim(),
    });
    setSubmitting(false);

    if (result.error) {
      setFormError(formatMutationError(result.error, 'Failed to create ingredient.'));
      return;
    }

    setName('');
    setCategory('');
    setDefaultUnit('');
    refetch({ requestPolicy: 'network-only' });
  }

  function startEdit(ingredient: Ingredient) {
    setEditingId(ingredient.id);
    setEditState(toEditState(ingredient));
    setRowError(null);
  }

  function cancelEdit() {
    setEditingId(null);
    setEditState(null);
  }

  async function handleSaveEdit(ingredientId: number) {
    if (!editState) return;
    if (!editState.name.trim() || !editState.defaultUnit.trim()) {
      setRowError({ id: ingredientId, message: 'Name and default unit are required.' });
      return;
    }

    const result = await updateIngredient({
      ingredientId,
      name: editState.name.trim(),
      category: editState.category.trim() || null,
      defaultUnit: editState.defaultUnit.trim(),
    });

    if (result.error) {
      setRowError({ id: ingredientId, message: formatMutationError(result.error, 'Failed to save ingredient.') });
      return;
    }

    setEditingId(null);
    setEditState(null);
    setRowError(null);
    refetch({ requestPolicy: 'network-only' });
  }

  async function handleDelete(ingredientId: number) {
    if (!window.confirm('Delete this ingredient? This cannot be undone.')) {
      return;
    }

    setRowError(null);
    const result = await deleteIngredient({ ingredientId });

    if (result.error) {
      setRowError({ id: ingredientId, message: formatMutationError(result.error, 'Failed to delete ingredient.') });
      return;
    }

    refetch({ requestPolicy: 'network-only' });
  }

  return (
    <div>
      <h1>Manage Ingredients</h1>

      <form className="pantry-form" onSubmit={handleCreate}>
        <label>
          Name
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label>
          Category (optional)
          <input type="text" value={category} onChange={(e) => setCategory(e.target.value)} />
        </label>
        <label>
          Default unit
          <input
            type="text"
            className="input-mono"
            value={defaultUnit}
            onChange={(e) => setDefaultUnit(e.target.value)}
          />
        </label>
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? 'Adding...' : 'Add ingredient'}
        </button>
      </form>
      {formError && <p className="error-message">{formError}</p>}

      {fetching && <p>Loading ingredients...</p>}
      {error && <p className="error-message">Failed to load ingredients: {error.message}</p>}
      {data?.ingredients.length === 0 && <p>No ingredients yet.</p>}

      {data?.ingredients.map((ingredient) => (
        <div key={ingredient.id} className="pantry-item-row">
          {editingId === ingredient.id && editState ? (
            <>
              <span className="field-row">
                <input
                  type="text"
                  value={editState.name}
                  onChange={(e) => setEditState({ ...editState, name: e.target.value })}
                />
                <input
                  type="text"
                  placeholder="Category"
                  value={editState.category}
                  onChange={(e) => setEditState({ ...editState, category: e.target.value })}
                />
                <input
                  type="text"
                  className="input-mono"
                  value={editState.defaultUnit}
                  onChange={(e) => setEditState({ ...editState, defaultUnit: e.target.value })}
                />
              </span>
              <span>
                <button type="button" className="btn btn-primary btn-sm" onClick={() => handleSaveEdit(ingredient.id)}>
                  Save
                </button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={cancelEdit}>
                  Cancel
                </button>
              </span>
            </>
          ) : (
            <>
              <span>
                {ingredient.name}
                {ingredient.category ? ` (${ingredient.category})` : ''} &middot; default unit:{' '}
                <span className="input-mono">{ingredient.defaultUnit}</span>
              </span>
              <span>
                <button type="button" className="btn btn-outline btn-sm" onClick={() => startEdit(ingredient)}>
                  Edit
                </button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => handleDelete(ingredient.id)}>
                  Delete
                </button>
              </span>
            </>
          )}
          {rowError?.id === ingredient.id && <p className="error-message">{rowError.message}</p>}
        </div>
      ))}
    </div>
  );
}
