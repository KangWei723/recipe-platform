import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useClient, useMutation, useQuery } from 'urql';
import { useIsAdmin } from '../auth/useIsAdmin';
import { ConfirmedStoreLookup } from '../components/ConfirmedStoreLookup';
import { KrogerResultsPanel, type KrogerStoreGroup } from '../components/KrogerResultsPanel';
import { NearbyStoresBrowser } from '../components/NearbyStoresBrowser';
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
import type { RecipeDetail, StoreOffer } from '../graphql/types';
import { useUnits } from '../graphql/useUnits';
import { distanceMiles } from '../utils/distance';
import { formatMutationError } from '../utils/errors';
import { formatQuantity } from '../utils/quantity';
import { useGeolocation } from '../utils/useGeolocation';

// Folds newly-found Kroger offers into the existing store groups, keyed by StoreId -- offers
// from different ingredient searches routinely resolve to the same nearest store (Kroger's
// location lookup depends only on lat/lng, not the ingredient), so this is the common case, not
// an edge case. Products are deduped by ProductId within a store so re-clicking "Check again" on
// the same ingredient doesn't add a repeat row.
function mergeKrogerOffers(
  groups: KrogerStoreGroup[],
  offers: StoreOffer[],
  userLat: number,
  userLng: number,
): KrogerStoreGroup[] {
  const next = groups.map((g) => ({ ...g, products: [...g.products] }));

  for (const offer of offers) {
    if (!offer.storeId || !offer.productId || !offer.productName) continue;

    let group = next.find((g) => g.storeId === offer.storeId);
    if (!group) {
      group = {
        storeId: offer.storeId,
        storeName: offer.storeName,
        lat: offer.lat,
        lng: offer.lng,
        distanceMiles:
          offer.lat !== null && offer.lng !== null ? distanceMiles(userLat, userLng, offer.lat, offer.lng) : null,
        products: [],
      };
      next.push(group);
    }

    if (!group.products.some((p) => p.productId === offer.productId)) {
      group.products.push({
        productId: offer.productId,
        productName: offer.productName,
        price: offer.price,
        currency: offer.currency,
      });
    }
  }

  return next;
}

export function RecipeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const recipeId = Number(id);
  const navigate = useNavigate();
  const client = useClient();
  const isAdmin = useIsAdmin();
  const { isFractionalFriendly } = useUnits();
  const { getLocation } = useGeolocation();

  // network-only, not the default cache-first: this recipe's ingredients carry an inPantry
  // flag computed from the user's pantry, which can change from an entirely different page
  // (PantryPage) that has no way to know which cached recipe(id) queries that affects -- unlike
  // the recipe-edit flow, there's no single query to target and invalidate, so this query has
  // to stop trusting its cache instead.
  const [{ data, fetching, error }] = useQuery<{ recipe: RecipeDetail | null }, { id: number }>({
    query: RECIPE_QUERY,
    variables: { id: recipeId },
    pause: Number.isNaN(recipeId),
    requestPolicy: 'network-only',
  });
  const [, deleteRecipe] = useMutation(DELETE_RECIPE_MUTATION);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [scaledServings, setScaledServings] = useState<number | null>(null);
  const [krogerGroups, setKrogerGroups] = useState<KrogerStoreGroup[]>([]);

  function handleKrogerFound(offers: StoreOffer[], userLat: number, userLng: number) {
    setKrogerGroups((prev) => mergeKrogerOffers(prev, offers, userLat, userLng));
  }

  // Resets the adjusted serving count whenever the loaded recipe changes -- navigating from one
  // recipe's detail page to another (a route param change, not a remount) would otherwise carry
  // over the previous recipe's scaling.
  useEffect(() => {
    setScaledServings(data?.recipe?.servings ?? null);
  }, [data?.recipe?.id, data?.recipe?.servings]);

  if (fetching) return <p>Loading recipe...</p>;
  if (error) return <p className="error-message">Failed to load recipe: {error.message}</p>;
  if (!data?.recipe) return <p>Recipe not found.</p>;

  const recipe = data.recipe;
  const sortedSteps = [...recipe.steps].sort((a, b) => a.stepNumber - b.stepNumber);
  const scaleRatio = recipe.servings && scaledServings ? scaledServings / recipe.servings : 1;

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

  const haveCount = recipe.ingredients.filter((i) => i.inPantry).length;

  return (
    <div>
      <div className="content-panel recipe-detail-panel">
        <div className="recipe-detail-main">
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
          <div className="recipe-meta">
            {recipe.prepTimeMin != null && <span>PREP {recipe.prepTimeMin}m</span>}
            {recipe.cookTimeMin != null && <span>COOK {recipe.cookTimeMin}m</span>}
            {recipe.servings != null && (
              <span className="servings-adjuster">
                SERVES
                <button
                  type="button"
                  aria-label="Decrease servings"
                  disabled={(scaledServings ?? recipe.servings) <= 1}
                  onClick={() => setScaledServings((s) => Math.max(1, (s ?? recipe.servings!) - 1))}
                >
                  &minus;
                </button>
                <span className="servings-value">{scaledServings ?? recipe.servings}</span>
                <button
                  type="button"
                  aria-label="Increase servings"
                  onClick={() => setScaledServings((s) => (s ?? recipe.servings!) + 1)}
                >
                  +
                </button>
                {scaledServings !== recipe.servings && (
                  <button type="button" className="servings-reset" onClick={() => setScaledServings(recipe.servings)}>
                    Reset
                  </button>
                )}
              </span>
            )}
          </div>

          <div className="section-divider" />

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

        <div className="recipe-detail-ingredients">
          <div className="panel-toolbar">
            <h2>Ingredients</h2>
            <span className="stat-line">
              {haveCount}/{recipe.ingredients.length} ON SHELF
            </span>
          </div>
          <KrogerResultsPanel groups={krogerGroups} />
          {haveCount < recipe.ingredients.length && <NearbyStoresBrowser getLocation={getLocation} />}
          {recipe.ingredients.map((ingredient) => {
            // The top-ranked substitute only -- ingredient.substitutions carries a full ranked
            // array (ratio/confidence/contexts per candidate), but a single best suggestion
            // reads more cleanly here than a list of alternatives.
            const topSub = ingredient.substitutions[0];
            return (
              <div key={ingredient.id}>
                <div className={`ingredient-row${ingredient.inPantry ? ' in-pantry' : ''}`}>
                  <span className="ingredient-dot" aria-hidden="true" />
                  <span className="sr-only">{ingredient.inPantry ? 'In pantry' : 'Missing'}</span>
                  <span className="ingredient-qty">
                    {formatQuantity(ingredient.quantity * scaleRatio, isFractionalFriendly(ingredient.unit))}{' '}
                    {ingredient.unit}
                  </span>
                  <span className="ingredient-name">
                    {ingredient.ingredientName}
                    {ingredient.optional ? ' (optional)' : ''}
                  </span>
                </div>
                {!ingredient.inPantry && (
                  <div className="missing-callout">
                    <span className="missing-callout-label">Not on the shelf</span>
                    {topSub && (
                      <p className="missing-callout-sub">
                        Try instead: <strong>{topSub.substituteName}</strong>
                      </p>
                    )}
                    <ConfirmedStoreLookup
                      ingredientName={ingredient.ingredientName}
                      getLocation={getLocation}
                      onFound={handleKrogerFound}
                    />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
