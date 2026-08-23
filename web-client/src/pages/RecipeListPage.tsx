import { useQuery } from 'urql';
import { Link } from 'react-router-dom';
import { useIsAdmin } from '../auth/useIsAdmin';
import { buttonVariants } from '../components/ui/button';
import { RECIPES_QUERY } from '../graphql/queries';
import type { RecipeSummary } from '../graphql/types';

export function RecipeListPage() {
  const isAdmin = useIsAdmin();
  const [{ data, fetching, error }] = useQuery<{ recipes: RecipeSummary[] }>({
    query: RECIPES_QUERY,
  });

  if (fetching) return <p>Loading recipes...</p>;
  if (error) return <p className="error-message">Failed to load recipes: {error.message}</p>;

  return (
    <div>
      <div className="page-header">
        <h1>Recipes</h1>
        {isAdmin && (
          <Link to="/recipes/new" className={buttonVariants({ variant: 'default' })}>
            Add Recipe
          </Link>
        )}
      </div>
      {data?.recipes.length === 0 && <p>No recipes yet.</p>}
      {data?.recipes.map((recipe) => (
        <Link key={recipe.id} to={`/recipes/${recipe.id}`} className="recipe-card">
          <h2>{recipe.title}</h2>
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
        </Link>
      ))}
    </div>
  );
}
