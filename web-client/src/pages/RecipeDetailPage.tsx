import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery } from 'urql';
import { useIsAdmin } from '../auth/useIsAdmin';
import { NearbyStoresFinder } from '../components/NearbyStoresFinder';
import { DELETE_RECIPE_MUTATION, RECIPE_QUERY } from '../graphql/queries';
import type { RecipeDetail } from '../graphql/types';
import { formatMutationError } from '../utils/errors';

export function RecipeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const recipeId = Number(id);
  const navigate = useNavigate();
  const isAdmin = useIsAdmin();

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
    if (!window.confirm(`Delete "${recipe.title}"? This cannot be undone.`)) {
      return;
    }

    setDeleteError(null);
    setDeleting(true);
    const result = await deleteRecipe({ recipeId });
    setDeleting(false);

    if (result.error) {
      setDeleteError(formatMutationError(result.error, 'Failed to delete recipe.'));
      return;
    }

    navigate('/');
  }

  return (
    <div>
      <h1>{recipe.title}</h1>
      {isAdmin && (
        <div className="field-row">
          <Link to={`/recipes/${recipe.id}/edit`} className="btn btn-outline btn-sm">
            Edit
          </Link>
          <button type="button" className="btn btn-ghost btn-sm" onClick={handleDelete} disabled={deleting}>
            {deleting ? 'Deleting...' : 'Delete'}
          </button>
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
              {ingredient.quantity} {ingredient.unit}
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
