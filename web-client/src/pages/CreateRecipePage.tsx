import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery } from 'urql';
import { CREATE_RECIPE_MUTATION, INGREDIENTS_QUERY } from '../graphql/queries';
import type { Ingredient } from '../graphql/types';

interface IngredientRow {
  key: number;
  ingredientId: string;
  quantity: string;
  unit: string;
  optional: boolean;
}

interface StepRow {
  key: number;
  instruction: string;
  timerSeconds: string;
}

let nextRowKey = 0;

function emptyIngredientRow(): IngredientRow {
  return { key: nextRowKey++, ingredientId: '', quantity: '', unit: '', optional: false };
}

function emptyStepRow(): StepRow {
  return { key: nextRowKey++, instruction: '', timerSeconds: '' };
}

export function CreateRecipePage() {
  const navigate = useNavigate();

  const [{ data: ingredientsData, fetching: ingredientsFetching }] = useQuery<{ ingredients: Ingredient[] }>({
    query: INGREDIENTS_QUERY,
  });
  const [, createRecipe] = useMutation<{ createRecipe: { id: number; title: string } }>(CREATE_RECIPE_MUTATION);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [servings, setServings] = useState('');
  const [prepTimeMin, setPrepTimeMin] = useState('');
  const [cookTimeMin, setCookTimeMin] = useState('');
  const [ingredientRows, setIngredientRows] = useState<IngredientRow[]>([emptyIngredientRow()]);
  const [stepRows, setStepRows] = useState<StepRow[]>([emptyStepRow()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const ingredients = ingredientsData?.ingredients ?? [];

  function updateIngredientRow(key: number, patch: Partial<IngredientRow>) {
    setIngredientRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  function handleIngredientSelect(key: number, ingredientId: string) {
    const selected = ingredients.find((i) => String(i.id) === ingredientId);
    updateIngredientRow(key, { ingredientId, unit: selected ? selected.defaultUnit : '' });
  }

  function updateStepRow(key: number, patch: Partial<StepRow>) {
    setStepRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!title.trim()) {
      setError('Title is required.');
      return;
    }
    if (ingredientRows.some((r) => !r.ingredientId || !r.quantity || !r.unit)) {
      setError('Every ingredient needs an ingredient, quantity, and unit.');
      return;
    }
    if (stepRows.some((r) => !r.instruction.trim())) {
      setError('Every step needs an instruction.');
      return;
    }

    setSubmitting(true);
    const result = await createRecipe({
      title: title.trim(),
      description: description.trim() || null,
      servings: servings ? Number(servings) : null,
      prepTimeMin: prepTimeMin ? Number(prepTimeMin) : null,
      cookTimeMin: cookTimeMin ? Number(cookTimeMin) : null,
      ingredients: ingredientRows.map((r) => ({
        ingredientId: Number(r.ingredientId),
        quantity: Number(r.quantity),
        unit: r.unit,
        optional: r.optional,
      })),
      steps: stepRows.map((r, index) => ({
        stepNumber: index + 1,
        instruction: r.instruction.trim(),
        timerSeconds: r.timerSeconds ? Number(r.timerSeconds) : null,
      })),
    });
    setSubmitting(false);

    if (result.error || !result.data) {
      setError(result.error?.message ?? 'Failed to create recipe.');
      return;
    }

    navigate(`/recipes/${result.data.createRecipe.id}`);
  }

  return (
    <div>
      <h1>Add Recipe</h1>
      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <label>
            Title
            <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} />
          </label>
        </div>
        <div className="form-field">
          <label>
            Description
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </label>
        </div>
        <div className="form-row">
          <label>
            Servings
            <input type="number" min="1" value={servings} onChange={(e) => setServings(e.target.value)} />
          </label>
          <label>
            Prep time (min)
            <input type="number" min="0" value={prepTimeMin} onChange={(e) => setPrepTimeMin(e.target.value)} />
          </label>
          <label>
            Cook time (min)
            <input type="number" min="0" value={cookTimeMin} onChange={(e) => setCookTimeMin(e.target.value)} />
          </label>
        </div>

        <h2>Ingredients</h2>
        {ingredientRows.map((row) => (
          <div key={row.key} className="pantry-form">
            <label>
              Ingredient
              <select
                value={row.ingredientId}
                onChange={(e) => handleIngredientSelect(row.key, e.target.value)}
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
                value={row.quantity}
                onChange={(e) => updateIngredientRow(row.key, { quantity: e.target.value })}
              />
            </label>
            <label>
              Unit
              <input
                type="text"
                value={row.unit}
                onChange={(e) => updateIngredientRow(row.key, { unit: e.target.value })}
              />
            </label>
            <label>
              Optional
              <input
                type="checkbox"
                checked={row.optional}
                onChange={(e) => updateIngredientRow(row.key, { optional: e.target.checked })}
              />
            </label>
            <button
              type="button"
              onClick={() => setIngredientRows((rows) => rows.filter((r) => r.key !== row.key))}
              disabled={ingredientRows.length === 1}
            >
              Remove
            </button>
          </div>
        ))}
        <button type="button" onClick={() => setIngredientRows((rows) => [...rows, emptyIngredientRow()])}>
          Add ingredient
        </button>

        <h2>Steps</h2>
        {stepRows.map((row, index) => (
          <div key={row.key} className="pantry-form">
            <label>
              Step {index + 1}
              <input
                type="text"
                value={row.instruction}
                onChange={(e) => updateStepRow(row.key, { instruction: e.target.value })}
              />
            </label>
            <label>
              Timer (seconds, optional)
              <input
                type="number"
                min="0"
                value={row.timerSeconds}
                onChange={(e) => updateStepRow(row.key, { timerSeconds: e.target.value })}
              />
            </label>
            <button
              type="button"
              onClick={() => setStepRows((rows) => rows.filter((r) => r.key !== row.key))}
              disabled={stepRows.length === 1}
            >
              Remove
            </button>
          </div>
        ))}
        <button type="button" onClick={() => setStepRows((rows) => [...rows, emptyStepRow()])}>
          Add step
        </button>

        {error && <p className="error-message">{error}</p>}
        <div className="form-field">
          <button type="submit" disabled={submitting}>
            {submitting ? 'Creating...' : 'Create Recipe'}
          </button>
        </div>
      </form>
    </div>
  );
}
