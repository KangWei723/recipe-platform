import { useParams } from 'react-router-dom';
import { useQuery } from 'urql';
import { NearbyStoresFinder } from '../components/NearbyStoresFinder';
import { RECIPE_QUERY } from '../graphql/queries';
import type { RecipeDetail } from '../graphql/types';

export function RecipeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const recipeId = Number(id);

  const [{ data, fetching, error }] = useQuery<{ recipe: RecipeDetail | null }, { id: number }>({
    query: RECIPE_QUERY,
    variables: { id: recipeId },
    pause: Number.isNaN(recipeId),
  });

  if (fetching) return <p>Loading recipe...</p>;
  if (error) return <p className="error-message">Failed to load recipe: {error.message}</p>;
  if (!data?.recipe) return <p>Recipe not found.</p>;

  const recipe = data.recipe;
  const sortedSteps = [...recipe.steps].sort((a, b) => a.stepNumber - b.stepNumber);

  return (
    <div>
      <h1>{recipe.title}</h1>
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
