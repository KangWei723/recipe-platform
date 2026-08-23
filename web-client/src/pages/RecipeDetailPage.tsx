import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useClient, useMutation, useQuery } from 'urql';
import { useIsAdmin } from '../auth/useIsAdmin';
import { NearbyStoresFinder } from '../components/NearbyStoresFinder';
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
import { Button, buttonVariants } from '../components/ui/button';
import { DELETE_RECIPE_MUTATION, RECIPE_QUERY, RECIPES_QUERY } from '../graphql/queries';
import type { RecipeDetail } from '../graphql/types';
import { useUnits } from '../graphql/useUnits';
import { formatMutationError } from '../utils/errors';
import { formatQuantity } from '../utils/quantity';

export function RecipeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const recipeId = Number(id);
  const navigate = useNavigate();
  const client = useClient();
  const isAdmin = useIsAdmin();
  const { isFractionalFriendly } = useUnits();

  const [{ data, fetching, error }] = useQuery<{ recipe: RecipeDetail | null }, { id: number }>({
    query: RECIPE_QUERY,
    variables: { id: recipeId },
    pause: Number.isNaN(recipeId),
  });
  const [, deleteRecipe] = useMutation(DELETE_RECIPE_MUTATION);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  if (fetching) return <p>Loading recipe...</p>;
  if (error) return <p className="error-message">Failed to load recipe: {error.message}</p>;
  if (!data?.recipe) return <p>Recipe not found.</p>;

  const recipe = data.recipe;
  const sortedSteps = [...recipe.steps].sort((a, b) => a.stepNumber - b.stepNumber);

  async function handleDelete() {
    setDeleteError(null);
    setDeleting(true);
    const result = await deleteRecipe({ recipeId });
    setDeleting(false);

    if (result.error) {
      setDeleteError(formatMutationError(result.error, 'Failed to delete recipe.'));
      return;
    }

    // Refetch before navigating so the list doesn't read urql's stale cache-first
    // entry and briefly show the just-deleted recipe.
    await client.query(RECIPES_QUERY, {}, { requestPolicy: 'network-only' }).toPromise();
    navigate('/');
  }

  return (
    <div>
      <h1>{recipe.title}</h1>
      {isAdmin && (
        <div className="field-row">
          <Link
            to={`/recipes/${recipe.id}/edit`}
            className={buttonVariants({ variant: 'outline', size: 'sm' })}
          >
            Edit
          </Link>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button type="button" variant="destructive" size="sm" disabled={deleting}>
                {deleting ? 'Deleting...' : 'Delete'}
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete "{recipe.title}"?</AlertDialogTitle>
                <AlertDialogDescription>This cannot be undone.</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      )}
      {deleteError && <p className="error-message">{deleteError}</p>}
      {recipe.description && <p>{recipe.description}</p>}
      <small>
        {[
          recipe.servings ? `${recipe.servings} servings` : null,
          recipe.prepTimeMin ? `${recipe.prepTimeMin} min prep` : null,
          recipe.cookTimeMin ? `${recipe.cookTimeMin} min cook` : null,
        ]
          .filter(Boolean)
          .join(' · ')}
      </small>

      <h2>Ingredients</h2>
      {recipe.ingredients.map((ingredient) => (
        <div key={ingredient.id}>
          <div className={`ingredient-row${ingredient.inPantry ? ' in-pantry' : ''}`}>
            <span className="ingredient-dot" aria-hidden="true" />
            <span className="sr-only">{ingredient.inPantry ? 'In pantry' : 'Missing'}</span>
            <span className="ingredient-qty">
              {formatQuantity(ingredient.quantity, isFractionalFriendly(ingredient.unit))} {ingredient.unit}
            </span>
            <span className="ingredient-name">
              {ingredient.ingredientName}
              {ingredient.optional ? ' (optional)' : ''}
            </span>
          </div>
          {!ingredient.inPantry && (
            <div className="missing-ingredient-options">
              {ingredient.substitutions.length > 0 && (
                <ul className="substitutions">
                  {ingredient.substitutions.map((sub) => (
                    <li key={sub.substituteName}>
                      Substitute: {sub.substituteName} (ratio {sub.ratio}, confidence{' '}
                      {Math.round(sub.confidence * 100)}%)
                    </li>
                  ))}
                </ul>
              )}
              <NearbyStoresFinder ingredientName={ingredient.ingredientName} />
            </div>
          )}
        </div>
      ))}

      <h2>Steps</h2>
      <ol className="steps-list">
        {sortedSteps.map((step) => (
          <li key={step.id}>
            {step.instruction}
            {step.timerSeconds && ` (${Math.round(step.timerSeconds / 60)} min)`}
          </li>
        ))}
      </ol>
    </div>
  );
}
