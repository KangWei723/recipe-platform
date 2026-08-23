import { useState, type FormEvent } from 'react';
import { useMutation, useQuery } from 'urql';
import { useIsAdmin } from '../auth/useIsAdmin';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '../components/ui/alert-dialog';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import {
  CREATE_INGREDIENT_MUTATION,
  DELETE_INGREDIENT_MUTATION,
  INGREDIENTS_QUERY,
  UPDATE_INGREDIENT_MUTATION,
} from '../graphql/queries';
import type { Ingredient } from '../graphql/types';
import { useIngredientCategories } from '../graphql/useIngredientCategories';
import { useUnits } from '../graphql/useUnits';
import { formatMutationError } from '../utils/errors';

interface EditState {
  name: string;
  category: string;
  defaultUnit: string;
}

function toEditState(ingredient: Ingredient): EditState {
  return { name: ingredient.name, category: ingredient.category, defaultUnit: ingredient.defaultUnit };
}

export function IngredientsPage() {
  const isAdmin = useIsAdmin();
  const { units, fetching: unitsFetching } = useUnits();
  const { categories, fetching: categoriesFetching, getLabel: getCategoryLabel } = useIngredientCategories();

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
  const [filter, setFilter] = useState('');

  if (!isAdmin) {
    return <p>Admin access required to manage ingredients.</p>;
  }

  const filteredIngredients = (data?.ingredients ?? []).filter((ingredient) =>
    ingredient.name.toLowerCase().includes(filter.trim().toLowerCase()),
  );

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);

    if (!name.trim() || !category || !defaultUnit.trim()) {
      setFormError('Name, category, and default unit are required.');
      return;
    }

    setSubmitting(true);
    const result = await createIngredient({
      name: name.trim(),
      category,
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
    if (!editState.name.trim() || !editState.category || !editState.defaultUnit.trim()) {
      setRowError({ id: ingredientId, message: 'Name, category, and default unit are required.' });
      return;
    }

    const result = await updateIngredient({
      ingredientId,
      name: editState.name.trim(),
      category: editState.category,
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
      <div className="page-header">
        <h1>Manage Ingredients</h1>
        <span className="keeper-badge">Keeper's desk</span>
      </div>

      <div className="content-panel keeper-panel">
        <div className="panel-toolbar">
          <Input
            type="search"
            className="filter-input"
            placeholder="Filter the catalog"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            aria-label="Filter the catalog"
          />
          <span className="stat-line">{data?.ingredients.length ?? 0} INGREDIENTS</span>
        </div>

      <form className="pantry-form" onSubmit={handleCreate}>
        <label>
          <Label htmlFor="new-ingredient-name">Name</Label>
          <Input id="new-ingredient-name" type="text" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label>
          <Label htmlFor="new-ingredient-category">Category</Label>
          <Select value={category} onValueChange={setCategory} disabled={categoriesFetching}>
            <SelectTrigger id="new-ingredient-category">
              <SelectValue placeholder="Select..." />
            </SelectTrigger>
            <SelectContent>
              {categories.map((c) => (
                <SelectItem key={c.code} value={c.code}>
                  {c.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
        <label>
          <Label htmlFor="new-ingredient-unit">Default unit</Label>
          <Select value={defaultUnit} onValueChange={setDefaultUnit} disabled={unitsFetching}>
            <SelectTrigger id="new-ingredient-unit" className="input-mono">
              <SelectValue placeholder="Select..." />
            </SelectTrigger>
            <SelectContent>
              {units.map((u) => (
                <SelectItem key={u.code} value={u.code}>
                  {u.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Adding...' : 'Add ingredient'}
        </Button>
      </form>
      {formError && <p className="error-message">{formError}</p>}

      {fetching && <p>Loading ingredients...</p>}
      {error && <p className="error-message">Failed to load ingredients: {error.message}</p>}
      {data?.ingredients.length === 0 && <p>No ingredients yet.</p>}
      {data && data.ingredients.length > 0 && filteredIngredients.length === 0 && (
        <p>No ingredients match "{filter}".</p>
      )}

      {filteredIngredients.length > 0 && (
        <div className="ingredient-table-header">
          <span>Name</span>
          <span>Category</span>
          <span>Default unit</span>
          <span>In recipes</span>
          <span />
        </div>
      )}

      {filteredIngredients.map((ingredient) => (
        <div
          key={ingredient.id}
          className={editingId === ingredient.id && editState ? 'field-row' : 'ingredient-table-row'}
        >
          {editingId === ingredient.id && editState ? (
            <>
              <span className="field-row">
                <Input
                  type="text"
                  value={editState.name}
                  onChange={(e) => setEditState({ ...editState, name: e.target.value })}
                />
                <Select
                  value={editState.category}
                  onValueChange={(value) => setEditState({ ...editState, category: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select..." />
                  </SelectTrigger>
                  <SelectContent>
                    {categories.map((c) => (
                      <SelectItem key={c.code} value={c.code}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select
                  value={editState.defaultUnit}
                  onValueChange={(value) => setEditState({ ...editState, defaultUnit: value })}
                >
                  <SelectTrigger className="input-mono">
                    <SelectValue placeholder="Select..." />
                  </SelectTrigger>
                  <SelectContent>
                    {units.map((u) => (
                      <SelectItem key={u.code} value={u.code}>
                        {u.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </span>
              <span>
                <Button type="button" size="sm" onClick={() => handleSaveEdit(ingredient.id)}>
                  Save
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={cancelEdit}>
                  Cancel
                </Button>
              </span>
            </>
          ) : (
            <>
              <span>{ingredient.name}</span>
              <span>{getCategoryLabel(ingredient.category)}</span>
              <span className="input-mono">{ingredient.defaultUnit}</span>
              <span className="input-mono">{ingredient.usageCount}</span>
              <span className="ingredient-table-row-actions">
                <Button type="button" variant="outline" size="sm" onClick={() => startEdit(ingredient)}>
                  Edit
                </Button>
                <AlertDialog>
                  <AlertDialogTrigger asChild>
                    <Button type="button" variant="destructive" size="sm">
                      Delete
                    </Button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Delete "{ingredient.name}"?</AlertDialogTitle>
                      <AlertDialogDescription>This cannot be undone.</AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Cancel</AlertDialogCancel>
                      <AlertDialogAction onClick={() => handleDelete(ingredient.id)}>Delete</AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              </span>
            </>
          )}
          {rowError?.id === ingredient.id && <p className="error-message">{rowError.message}</p>}
        </div>
      ))}
      </div>
    </div>
  );
}
