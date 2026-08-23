import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useClient, useMutation, useQuery } from 'urql';
import { Button } from '../components/ui/button';
import { Checkbox } from '../components/ui/checkbox';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Textarea } from '../components/ui/textarea';
import {
  CREATE_RECIPE_MUTATION,
  INGREDIENTS_QUERY,
  RECIPE_QUERY,
  RECIPES_QUERY,
  UPDATE_RECIPE_MUTATION,
} from '../graphql/queries';
import type { Ingredient, RecipeDetail } from '../graphql/types';
import { useUnits } from '../graphql/useUnits';
import { formatMutationError } from '../utils/errors';
import { formatQuantity, parseQuantityInput } from '../utils/quantity';

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

interface RecipeFormVariables {
  title: string;
  description: string | null;
  servings: number | null;
  prepTimeMin: number | null;
  cookTimeMin: number | null;
  ingredients: { ingredientId: number; quantity: number; unit: string; optional: boolean }[];
  steps: { stepNumber: number; instruction: string; timerSeconds: number | null }[];
}

let nextRowKey = 0;

function emptyIngredientRow(): IngredientRow {
  return { key: nextRowKey++, ingredientId: '', quantity: '', unit: '', optional: false };
}

function emptyStepRow(): StepRow {
  return { key: nextRowKey++, instruction: '', timerSeconds: '' };
}

export function RecipeFormPage() {
  const navigate = useNavigate();
  const client = useClient();
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const recipeId = Number(id);

  const [{ data: recipeData, fetching: recipeFetching, error: recipeError }] = useQuery<
    { recipe: RecipeDetail | null },
    { id: number }
  >({
    query: RECIPE_QUERY,
    variables: { id: recipeId },
    pause: !isEdit || Number.isNaN(recipeId),
  });

  const [{ data: ingredientsData, fetching: ingredientsFetching }] = useQuery<{ ingredients: Ingredient[] }>({
    query: INGREDIENTS_QUERY,
  });
  const { isFractionalFriendly, fetching: unitsFetching } = useUnits();
  const [, createRecipe] = useMutation<{ createRecipe: { id: number; title: string } }>(CREATE_RECIPE_MUTATION);
  const [, updateRecipe] = useMutation<{ updateRecipe: { id: number; title: string } }>(UPDATE_RECIPE_MUTATION);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [servings, setServings] = useState('');
  const [prepTimeMin, setPrepTimeMin] = useState('');
  const [cookTimeMin, setCookTimeMin] = useState('');
  const [ingredientRows, setIngredientRows] = useState<IngredientRow[]>([emptyIngredientRow()]);
  const [stepRows, setStepRows] = useState<StepRow[]>([emptyStepRow()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [prefilled, setPrefilled] = useState(false);

  const ingredients = ingredientsData?.ingredients ?? [];

  useEffect(() => {
    if (!isEdit || prefilled || !recipeData?.recipe || unitsFetching) return;

    const recipe = recipeData.recipe;
    setTitle(recipe.title);
    setDescription(recipe.description ?? '');
    setServings(recipe.servings != null ? String(recipe.servings) : '');
    setPrepTimeMin(recipe.prepTimeMin != null ? String(recipe.prepTimeMin) : '');
    setCookTimeMin(recipe.cookTimeMin != null ? String(recipe.cookTimeMin) : '');
    setIngredientRows(
      recipe.ingredients.length > 0
        ? recipe.ingredients.map((i) => ({
            key: nextRowKey++,
            ingredientId: String(i.ingredientId),
            quantity: formatQuantity(i.quantity, isFractionalFriendly(i.unit)),
            unit: i.unit,
            optional: i.optional,
          }))
        : [emptyIngredientRow()],
    );
    setStepRows(
      recipe.steps.length > 0
        ? [...recipe.steps]
            .sort((a, b) => a.stepNumber - b.stepNumber)
            .map((s) => ({
              key: nextRowKey++,
              instruction: s.instruction,
              timerSeconds: s.timerSeconds != null ? String(s.timerSeconds) : '',
            }))
        : [emptyStepRow()],
    );
    setPrefilled(true);
  }, [isEdit, prefilled, recipeData, unitsFetching, isFractionalFriendly]);

  function updateIngredientRow(key: number, patch: Partial<IngredientRow>) {
    setIngredientRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  function handleIngredientSelect(key: number, ingredientId: string) {
    const selected = ingredients.find((i) => String(i.id) === ingredientId);
    const newUnit = selected ? selected.defaultUnit : '';
    setIngredientRows((rows) =>
      rows.map((row) => {
        if (row.key !== key) return row;
        // A quantity typed under the old unit's style (e.g. a mixed number like "2 1/2" for a
        // fractional-friendly unit) isn't valid input once the unit style changes, so drop it
        // rather than carry over a string the new input can't parse.
        const categoryChanged = isFractionalFriendly(row.unit) !== isFractionalFriendly(newUnit);
        return { ...row, ingredientId, unit: newUnit, quantity: categoryChanged ? '' : row.quantity };
      }),
    );
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
    const parsedQuantities = ingredientRows.map((r) => parseQuantityInput(r.quantity, isFractionalFriendly(r.unit)));
    if (parsedQuantities.some((q) => q === null)) {
      setError('Quantity must be a number (e.g. "2" or, for this unit, a mixed number like "2 1/2").');
      return;
    }
    if (stepRows.some((r) => !r.instruction.trim())) {
      setError('Every step needs an instruction.');
      return;
    }

    const variables = {
      title: title.trim(),
      description: description.trim() || null,
      servings: servings ? Number(servings) : null,
      prepTimeMin: prepTimeMin ? Number(prepTimeMin) : null,
      cookTimeMin: cookTimeMin ? Number(cookTimeMin) : null,
      ingredients: ingredientRows.map((r, index) => ({
        ingredientId: Number(r.ingredientId),
        quantity: parsedQuantities[index] as number,
        unit: r.unit,
        optional: r.optional,
      })),
      steps: stepRows.map((r, index) => ({
        stepNumber: index + 1,
        instruction: r.instruction.trim(),
        timerSeconds: r.timerSeconds ? Number(r.timerSeconds) : null,
      })),
    };

    setSubmitting(true);
    const savedId = isEdit
      ? await submitUpdate(recipeId, variables)
      : await submitCreate(variables);
    setSubmitting(false);

    if (savedId !== undefined) {
      // Refetch from the network before navigating so the detail page (and the recipe
      // list, whose title/timing summary may have changed) don't read urql's stale
      // cache-first entry for this query+variables pair.
      await Promise.all([
        client.query(RECIPE_QUERY, { id: savedId }, { requestPolicy: 'network-only' }).toPromise(),
        client.query(RECIPES_QUERY, {}, { requestPolicy: 'network-only' }).toPromise(),
      ]);
      navigate(`/recipes/${savedId}`);
    }
  }

  async function submitCreate(variables: RecipeFormVariables): Promise<number | undefined> {
    const result = await createRecipe(variables);
    if (result.error) {
      setError(formatMutationError(result.error, 'Failed to create recipe.'));
      return undefined;
    }
    return result.data?.createRecipe.id;
  }

  async function submitUpdate(id: number, variables: RecipeFormVariables): Promise<number | undefined> {
    const result = await updateRecipe({ recipeId: id, ...variables });
    if (result.error) {
      setError(formatMutationError(result.error, 'Failed to save recipe.'));
      return undefined;
    }
    return result.data?.updateRecipe.id;
  }

  if (isEdit && recipeFetching && !prefilled) return <p>Loading recipe...</p>;
  if (isEdit && recipeError) return <p className="error-message">Failed to load recipe: {recipeError.message}</p>;
  if (isEdit && !recipeData?.recipe && !recipeFetching) return <p>Recipe not found.</p>;

  return (
    <div>
      <h1>{isEdit ? 'Edit Recipe' : 'Add Recipe'}</h1>
      <form onSubmit={handleSubmit}>
        <div className="form-field">
          <Label htmlFor="recipe-title">Title</Label>
          <Input id="recipe-title" type="text" value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>
        <div className="form-field">
          <Label htmlFor="recipe-description">Description</Label>
          <Textarea
            id="recipe-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
        <div className="form-row">
          <label>
            <Label htmlFor="recipe-servings">Servings</Label>
            <Input
              id="recipe-servings"
              type="number"
              min="1"
              value={servings}
              onChange={(e) => setServings(e.target.value)}
            />
          </label>
          <label>
            <Label htmlFor="recipe-prep-time">Prep time (min)</Label>
            <Input
              id="recipe-prep-time"
              type="number"
              min="0"
              value={prepTimeMin}
              onChange={(e) => setPrepTimeMin(e.target.value)}
            />
          </label>
          <label>
            <Label htmlFor="recipe-cook-time">Cook time (min)</Label>
            <Input
              id="recipe-cook-time"
              type="number"
              min="0"
              value={cookTimeMin}
              onChange={(e) => setCookTimeMin(e.target.value)}
            />
          </label>
        </div>

        <h2>Ingredients</h2>
        {ingredientRows.map((row) => (
          <div key={row.key} className="field-row">
            <label>
              <Label htmlFor={`ingredient-${row.key}-select`}>Ingredient</Label>
              <Select
                value={row.ingredientId}
                onValueChange={(value) => handleIngredientSelect(row.key, value)}
                disabled={ingredientsFetching}
              >
                <SelectTrigger id={`ingredient-${row.key}-select`}>
                  <SelectValue placeholder="Select..." />
                </SelectTrigger>
                <SelectContent>
                  {ingredients.map((i) => (
                    <SelectItem key={i.id} value={String(i.id)}>
                      {i.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </label>
            <label>
              <Label htmlFor={`ingredient-${row.key}-quantity`}>Quantity</Label>
              {isFractionalFriendly(row.unit) ? (
                <Input
                  id={`ingredient-${row.key}-quantity`}
                  type="text"
                  className="input-mono"
                  placeholder="e.g. 2 1/2"
                  value={row.quantity}
                  onChange={(e) => updateIngredientRow(row.key, { quantity: e.target.value })}
                />
              ) : (
                <Input
                  id={`ingredient-${row.key}-quantity`}
                  type="number"
                  min="0"
                  step="any"
                  className="input-mono"
                  value={row.quantity}
                  onChange={(e) => updateIngredientRow(row.key, { quantity: e.target.value })}
                />
              )}
            </label>
            <label>
              <Label htmlFor={`ingredient-${row.key}-unit`}>Unit</Label>
              <Input id={`ingredient-${row.key}-unit`} type="text" className="input-mono" value={row.unit} readOnly />
            </label>
            <label>
              <Checkbox
                checked={row.optional}
                onCheckedChange={(checked) => updateIngredientRow(row.key, { optional: checked === true })}
              />
              Optional
            </label>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setIngredientRows((rows) => rows.filter((r) => r.key !== row.key))}
              disabled={ingredientRows.length === 1}
            >
              Remove
            </Button>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setIngredientRows((rows) => [...rows, emptyIngredientRow()])}
        >
          Add ingredient
        </Button>

        <h2>Steps</h2>
        {stepRows.map((row, index) => (
          <div key={row.key} className="field-row">
            <label>
              <Label htmlFor={`step-${row.key}-instruction`}>Step {index + 1}</Label>
              <Input
                id={`step-${row.key}-instruction`}
                type="text"
                value={row.instruction}
                onChange={(e) => updateStepRow(row.key, { instruction: e.target.value })}
              />
            </label>
            <label>
              <Label htmlFor={`step-${row.key}-timer`}>Timer (seconds, optional)</Label>
              <Input
                id={`step-${row.key}-timer`}
                type="number"
                min="0"
                value={row.timerSeconds}
                onChange={(e) => updateStepRow(row.key, { timerSeconds: e.target.value })}
              />
            </label>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setStepRows((rows) => rows.filter((r) => r.key !== row.key))}
              disabled={stepRows.length === 1}
            >
              Remove
            </Button>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setStepRows((rows) => [...rows, emptyStepRow()])}
        >
          Add step
        </Button>

        {error && <p className="error-message">{error}</p>}
        <div className="form-field">
          <Button type="submit" disabled={submitting}>
            {submitting ? (isEdit ? 'Saving...' : 'Creating...') : isEdit ? 'Save Changes' : 'Create Recipe'}
          </Button>
        </div>
      </form>
    </div>
  );
}
