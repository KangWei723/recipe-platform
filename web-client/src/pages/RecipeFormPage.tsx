import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical } from 'lucide-react';
import { useEffect, useRef, useState, type CSSProperties, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useClient, useMutation, useQuery } from 'urql';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '../components/ui/alert-dialog';
import { Button } from '../components/ui/button';
import { Checkbox } from '../components/ui/checkbox';
import { ImageUrlPreview } from '../components/ImageUrlPreview';
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
  imageUrl: string;
  // Lives here, not in ImageUrlPreview's own state -- it has to move with the row through
  // drag-reorder (arrayMove) and deletion (filter) the same way imageUrl does. UI-only (never
  // sent to the server, never part of serializeSnapshot/isDirty), so there's nothing to persist
  // for an already-saved step -- it's always null until this session uploads a new image for it.
  imageFileName: string | null;
}

interface RecipeFormVariables {
  title: string;
  description: string | null;
  servings: number | null;
  prepTimeMin: number | null;
  cookTimeMin: number | null;
  ingredients: { ingredientId: number; quantity: number; unit: string; optional: boolean }[];
  steps: { stepNumber: number; instruction: string; timerSeconds: number | null; imageUrl: string | null }[];
  imageUrl: string | null;
  tips: string[];
  pairing: string | null;
}

let nextRowKey = 0;

function emptyIngredientRow(): IngredientRow {
  return { key: nextRowKey++, ingredientId: '', quantity: '', unit: '', optional: false };
}

function emptyStepRow(): StepRow {
  return { key: nextRowKey++, instruction: '', timerSeconds: '', imageUrl: '', imageFileName: null };
}

function emptyTipRow(): TipRow {
  return { key: nextRowKey++, text: '' };
}

interface TipRow {
  key: number;
  text: string;
}

interface FormSnapshotInput {
  title: string;
  description: string;
  servings: string;
  prepTimeMin: string;
  cookTimeMin: string;
  heroImageUrl: string;
  pairing: string;
  ingredientRows: IngredientRow[];
  stepRows: StepRow[];
  tipRows: TipRow[];
}

// Row `key` is an internal React identity, not user-visible content, so it's excluded here --
// otherwise every snapshot would compare unequal just because nextRowKey keeps incrementing.
function serializeSnapshot(state: FormSnapshotInput): string {
  return JSON.stringify({
    title: state.title,
    description: state.description,
    servings: state.servings,
    prepTimeMin: state.prepTimeMin,
    cookTimeMin: state.cookTimeMin,
    heroImageUrl: state.heroImageUrl,
    pairing: state.pairing,
    ingredients: state.ingredientRows.map(({ ingredientId, quantity, unit, optional }) => ({
      ingredientId,
      quantity,
      unit,
      optional,
    })),
    steps: state.stepRows.map(({ instruction, timerSeconds, imageUrl }) => ({ instruction, timerSeconds, imageUrl })),
    tips: state.tipRows.map(({ text }) => text),
  });
}

// The known shape of a brand-new Add Recipe form, used as the "untouched" baseline for create
// mode -- unlike edit mode, there's no async prefill to wait for, so this is available upfront.
const EMPTY_FORM_SNAPSHOT = serializeSnapshot({
  title: '',
  description: '',
  servings: '',
  prepTimeMin: '',
  cookTimeMin: '',
  heroImageUrl: '',
  pairing: '',
  ingredientRows: [{ key: 0, ingredientId: '', quantity: '', unit: '', optional: false }],
  stepRows: [{ key: 0, instruction: '', timerSeconds: '', imageUrl: '', imageFileName: null }],
  tipRows: [],
});

interface SortableStepRowProps {
  row: StepRow;
  index: number;
  onUpdate: (key: number, patch: Partial<StepRow>) => void;
  onRemove: (key: number) => void;
  removeDisabled: boolean;
}

// A standalone component (not inlined in the .map callback) because useSortable is a hook --
// calling it once per row requires each row to be its own component instance.
function SortableStepRow({ row, index, onUpdate, onRemove, removeDisabled }: SortableStepRowProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: row.key });

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div ref={setNodeRef} style={style} className="field-row step-row">
      <button
        type="button"
        className="step-drag-handle"
        aria-label={`Reorder step ${index + 1}`}
        {...attributes}
        {...listeners}
      >
        <GripVertical aria-hidden="true" />
      </button>
      <span className="step-number" aria-hidden="true">
        Step {index + 1}
      </span>
      <label className="step-instruction">
        <Label htmlFor={`step-${row.key}-instruction`}>Instruction</Label>
        <Textarea
          id={`step-${row.key}-instruction`}
          rows={3}
          aria-label={`Step ${index + 1} instruction`}
          value={row.instruction}
          onChange={(e) => onUpdate(row.key, { instruction: e.target.value })}
        />
      </label>
      <label>
        <Label htmlFor={`step-${row.key}-timer`}>Timer (seconds, optional)</Label>
        <Input
          id={`step-${row.key}-timer`}
          type="number"
          min="0"
          value={row.timerSeconds}
          onChange={(e) => onUpdate(row.key, { timerSeconds: e.target.value })}
        />
      </label>
      <ImageUrlPreview
        id={`step-${row.key}-image`}
        label="Step image (optional)"
        value={row.imageUrl}
        fileName={row.imageFileName}
        onChange={(value, fileName) => onUpdate(row.key, { imageUrl: value, imageFileName: fileName })}
      />
      <Button type="button" variant="ghost" size="sm" onClick={() => onRemove(row.key)} disabled={removeDisabled}>
        Remove
      </Button>
    </div>
  );
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
  const { units, isFractionalFriendly, fetching: unitsFetching } = useUnits();
  const stepSensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const [, createRecipe] = useMutation<{ createRecipe: { id: number; title: string } }>(CREATE_RECIPE_MUTATION);
  const [, updateRecipe] = useMutation<{ updateRecipe: { id: number; title: string } }>(UPDATE_RECIPE_MUTATION);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [servings, setServings] = useState('');
  const [prepTimeMin, setPrepTimeMin] = useState('');
  const [cookTimeMin, setCookTimeMin] = useState('');
  const [heroImageUrl, setHeroImageUrl] = useState('');
  // UI-only, same rationale as StepRow.imageFileName -- never sent to the server, never part of
  // serializeSnapshot/isDirty, always null for an already-saved recipe's hero image.
  const [heroImageFileName, setHeroImageFileName] = useState<string | null>(null);
  const [pairing, setPairing] = useState('');
  const [ingredientRows, setIngredientRows] = useState<IngredientRow[]>([emptyIngredientRow()]);
  const [stepRows, setStepRows] = useState<StepRow[]>([emptyStepRow()]);
  const [tipRows, setTipRows] = useState<TipRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [prefilled, setPrefilled] = useState(false);
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);

  // The "untouched form" baseline Cancel compares against to decide whether to confirm before
  // discarding. Create mode knows this upfront (EMPTY_FORM_SNAPSHOT); edit mode can't know it
  // until the recipe has loaded and prefilled the fields below, so it starts null and is filled
  // in at the end of the prefill effect.
  const baselineRef = useRef<string | null>(isEdit ? null : EMPTY_FORM_SNAPSHOT);

  const ingredients = ingredientsData?.ingredients ?? [];

  useEffect(() => {
    if (!isEdit || prefilled || !recipeData?.recipe || unitsFetching) return;

    const recipe = recipeData.recipe;
    const nextTitle = recipe.title;
    const nextDescription = recipe.description ?? '';
    const nextServings = recipe.servings != null ? String(recipe.servings) : '';
    const nextPrepTimeMin = recipe.prepTimeMin != null ? String(recipe.prepTimeMin) : '';
    const nextCookTimeMin = recipe.cookTimeMin != null ? String(recipe.cookTimeMin) : '';
    const nextHeroImageUrl = recipe.imageUrl ?? '';
    const nextPairing = recipe.pairing ?? '';
    const nextIngredientRows =
      recipe.ingredients.length > 0
        ? recipe.ingredients.map((i) => ({
            key: nextRowKey++,
            ingredientId: String(i.ingredientId),
            quantity: formatQuantity(i.quantity, isFractionalFriendly(i.unit)),
            unit: i.unit,
            optional: i.optional,
          }))
        : [emptyIngredientRow()];
    const nextStepRows =
      recipe.steps.length > 0
        ? [...recipe.steps]
            .sort((a, b) => a.stepNumber - b.stepNumber)
            .map((s) => ({
              key: nextRowKey++,
              instruction: s.instruction,
              timerSeconds: s.timerSeconds != null ? String(s.timerSeconds) : '',
              imageUrl: s.imageUrl ?? '',
              imageFileName: null, // never known for an already-saved step -- only the URL is persisted
            }))
        : [emptyStepRow()];
    const nextTipRows = recipe.tips.map((text) => ({ key: nextRowKey++, text }));

    setTitle(nextTitle);
    setDescription(nextDescription);
    setServings(nextServings);
    setPrepTimeMin(nextPrepTimeMin);
    setCookTimeMin(nextCookTimeMin);
    setHeroImageUrl(nextHeroImageUrl);
    setHeroImageFileName(null);
    setPairing(nextPairing);
    setIngredientRows(nextIngredientRows);
    setStepRows(nextStepRows);
    setTipRows(nextTipRows);
    setPrefilled(true);

    baselineRef.current = serializeSnapshot({
      title: nextTitle,
      description: nextDescription,
      servings: nextServings,
      prepTimeMin: nextPrepTimeMin,
      cookTimeMin: nextCookTimeMin,
      heroImageUrl: nextHeroImageUrl,
      pairing: nextPairing,
      ingredientRows: nextIngredientRows,
      stepRows: nextStepRows,
      tipRows: nextTipRows,
    });
  }, [isEdit, prefilled, recipeData, unitsFetching, isFractionalFriendly]);

  function isDirty(): boolean {
    // Baseline isn't established yet (edit mode still loading) -- nothing to compare against,
    // so there's nothing the user could have changed.
    if (baselineRef.current === null) return false;
    return (
      serializeSnapshot({
        title,
        description,
        servings,
        prepTimeMin,
        cookTimeMin,
        heroImageUrl,
        pairing,
        ingredientRows,
        stepRows,
        tipRows,
      }) !== baselineRef.current
    );
  }

  function navigateAway() {
    navigate(isEdit ? `/recipes/${recipeId}` : '/');
  }

  function handleCancelClick() {
    if (isDirty()) {
      setCancelDialogOpen(true);
    } else {
      navigateAway();
    }
  }

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

  function handleUnitSelect(key: number, unit: string) {
    setIngredientRows((rows) =>
      rows.map((row) => {
        if (row.key !== key) return row;
        // Same reset rule as handleIngredientSelect: the admin can now pick a unit independent
        // of the ingredient's default_unit, so a fractional-vs-decimal style change has to be
        // handled here too, not just when the ingredient itself changes.
        const categoryChanged = isFractionalFriendly(row.unit) !== isFractionalFriendly(unit);
        return { ...row, unit, quantity: categoryChanged ? '' : row.quantity };
      }),
    );
  }

  function updateStepRow(key: number, patch: Partial<StepRow>) {
    setStepRows((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  function updateTipRow(key: number, text: string) {
    setTipRows((rows) => rows.map((row) => (row.key === key ? { ...row, text } : row)));
  }

  // Reorders by moving one row to another position rather than touching row content, so each
  // row keeps its own React key (and DOM node/focus) as it moves -- stepNumber itself is never
  // stored, only derived from array position at submit time (see handleSubmit's
  // `steps: stepRows.map((r, index) => ...)`), so dragging a row immediately renumbers every
  // row's visible "Step N" label without any separate reindexing step.
  function handleStepDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    setStepRows((rows) => {
      const oldIndex = rows.findIndex((r) => r.key === active.id);
      const newIndex = rows.findIndex((r) => r.key === over.id);
      return arrayMove(rows, oldIndex, newIndex);
    });
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
      imageUrl: heroImageUrl.trim() || null,
      pairing: pairing.trim() || null,
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
        imageUrl: r.imageUrl.trim() || null,
      })),
      // Blank rows (added via "Add tip" then left empty) are dropped rather than rejected --
      // same forgiving treatment as an unfilled optional field elsewhere in this form.
      tips: tipRows.map((r) => r.text.trim()).filter((text) => text.length > 0),
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
      <form className="content-panel keeper-panel" onSubmit={handleSubmit}>
        <div className="form-title-row">
          <div className="form-field">
            <Label htmlFor="recipe-title">Title</Label>
            <Input id="recipe-title" type="text" value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div className="form-field">
            <Label>Prep / Cook / Serves</Label>
            <div className="form-title-row-meta">
              <Input
                id="recipe-prep-time"
                type="number"
                min="0"
                className="input-mono"
                placeholder="Prep"
                aria-label="Prep time (min)"
                value={prepTimeMin}
                onChange={(e) => setPrepTimeMin(e.target.value)}
              />
              <Input
                id="recipe-cook-time"
                type="number"
                min="0"
                className="input-mono"
                placeholder="Cook"
                aria-label="Cook time (min)"
                value={cookTimeMin}
                onChange={(e) => setCookTimeMin(e.target.value)}
              />
              <Input
                id="recipe-servings"
                type="number"
                min="1"
                className="input-mono"
                placeholder="Serves"
                aria-label="Servings"
                value={servings}
                onChange={(e) => setServings(e.target.value)}
              />
            </div>
          </div>
        </div>
        <div className="form-field">
          <Label htmlFor="recipe-description">Description</Label>
          <Textarea
            id="recipe-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
        <ImageUrlPreview
          id="recipe-hero-image"
          label="Hero image (optional)"
          value={heroImageUrl}
          fileName={heroImageFileName}
          onChange={(value, fileName) => {
            setHeroImageUrl(value);
            setHeroImageFileName(fileName);
          }}
        />

        <div className="form-columns">
        <div>
        <h2>Ingredients</h2>
        {ingredientRows.map((row) => (
          <div key={row.key} className="field-row ingredient-field-row">
            <label>
              <Label htmlFor={`ingredient-${row.key}-select`}>Ingredient</Label>
              <Select
                value={row.ingredientId}
                onValueChange={(value) => handleIngredientSelect(row.key, value)}
                disabled={ingredientsFetching}
              >
                <SelectTrigger id={`ingredient-${row.key}-select`} className="w-full">
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
              <Select
                value={row.unit}
                onValueChange={(value) => handleUnitSelect(row.key, value)}
                disabled={unitsFetching}
              >
                <SelectTrigger id={`ingredient-${row.key}-unit`} className="input-mono w-full">
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
        </div>

        <div>
        <h2>Steps</h2>
        <DndContext sensors={stepSensors} collisionDetection={closestCenter} onDragEnd={handleStepDragEnd}>
          <SortableContext items={stepRows.map((r) => r.key)} strategy={verticalListSortingStrategy}>
            {stepRows.map((row, index) => (
              <SortableStepRow
                key={row.key}
                row={row}
                index={index}
                onUpdate={updateStepRow}
                onRemove={(key) => setStepRows((rows) => rows.filter((r) => r.key !== key))}
                removeDisabled={stepRows.length === 1}
              />
            ))}
          </SortableContext>
        </DndContext>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setStepRows((rows) => [...rows, emptyStepRow()])}
        >
          Add step
        </Button>
        </div>
        </div>

        <h2>Tips</h2>
        {tipRows.map((row, index) => (
          <div key={row.key} className="field-row tip-row">
            <label className="tip-row-input">
              <Label htmlFor={`tip-${row.key}`}>{`Tip ${index + 1}`}</Label>
              <Input
                id={`tip-${row.key}`}
                aria-label="Tip"
                value={row.text}
                onChange={(e) => updateTipRow(row.key, e.target.value)}
              />
            </label>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setTipRows((rows) => rows.filter((r) => r.key !== row.key))}
            >
              Remove
            </Button>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setTipRows((rows) => [...rows, emptyTipRow()])}
        >
          Add tip
        </Button>

        <div className="form-field">
          <Label htmlFor="recipe-pairing">Pairing (optional)</Label>
          <Input id="recipe-pairing" value={pairing} onChange={(e) => setPairing(e.target.value)} />
        </div>

        {error && <p className="error-message">{error}</p>}
        <div className="form-actions">
          <Button type="submit" disabled={submitting}>
            {submitting ? (isEdit ? 'Saving...' : 'Creating...') : isEdit ? 'Save Changes' : 'Create Recipe'}
          </Button>
          <Button type="button" variant="outline" onClick={handleCancelClick} disabled={submitting}>
            Cancel
          </Button>
        </div>
      </form>

      <AlertDialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard changes?</AlertDialogTitle>
            <AlertDialogDescription>
              {isEdit ? "Your changes to this recipe" : 'This recipe'} will not be saved.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={navigateAway}>Discard</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
