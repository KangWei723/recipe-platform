import { useState } from 'react';
import { useClient } from 'urql';
import { CONFIRMED_STORE_QUERY } from '../graphql/queries';
import type { StoreOffer } from '../graphql/types';
import type { GeolocationResult } from '../utils/useGeolocation';

type LookupState =
  | { status: 'idle' }
  | { status: 'locating' }
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'not-found' }
  | { status: 'found' };

// Per-ingredient, confirmed lookup -- today this only ever surfaces Kroger's real product/price
// results (SourcingService.FindConfirmedAsync only calls providers where IsIngredientSpecific is
// true), never a simulated placeholder, so a result here is presented as "found," not "might have
// it." Compare NearbyStoresBrowser, the once-per-page general/unconfirmed counterpart.
//
// Doesn't render the found products itself -- they're handed to onFound, which
// RecipeDetailPage accumulates into one shared, store-grouped KrogerResultsPanel (see there),
// since results across different ingredient clicks (and multiple products per click) commonly
// resolve to the same store and shouldn't be shown as disconnected, repeated lists.
export function ConfirmedStoreLookup({
  ingredientName,
  getLocation,
  onFound,
}: {
  ingredientName: string;
  getLocation: () => Promise<GeolocationResult>;
  onFound: (offers: StoreOffer[], userLat: number, userLng: number) => void;
}) {
  const client = useClient();
  const [state, setState] = useState<LookupState>({ status: 'idle' });

  async function handleFindStore() {
    setState({ status: 'locating' });
    const location = await getLocation();
    if (!location.ok) {
      setState({ status: 'error', message: location.message });
      return;
    }

    const { lat, lng } = location.coords;
    setState({ status: 'loading' });
    const result = await client
      .query<{ confirmedStoreOffer: StoreOffer[] }>(CONFIRMED_STORE_QUERY, { ingredientName, lat, lng })
      .toPromise();

    if (result.error || !result.data) {
      setState({ status: 'error', message: 'Could not check Kroger right now.' });
      return;
    }

    const offers = result.data.confirmedStoreOffer;
    if (offers.length === 0) {
      setState({ status: 'not-found' });
      return;
    }

    onFound(offers, lat, lng);
    setState({ status: 'found' });
  }

  if (state.status === 'locating') {
    return <p className="nearby-stores-status">Getting your location...</p>;
  }

  if (state.status === 'loading') {
    return <p className="nearby-stores-status">Checking Kroger...</p>;
  }

  if (state.status === 'not-found') {
    return (
      <div>
        <p className="nearby-stores-status">Not found at Kroger nearby.</p>
        <button type="button" className="find-nearby-link" onClick={handleFindStore}>
          Check again
        </button>
      </div>
    );
  }

  if (state.status === 'found') {
    return (
      <div>
        <p className="nearby-stores-status">Found at Kroger — see results above.</p>
        <button type="button" className="find-nearby-link" onClick={handleFindStore}>
          Check again
        </button>
      </div>
    );
  }

  return (
    <div>
      <button type="button" className="find-nearby-link" onClick={handleFindStore}>
        Find it at Kroger →
      </button>
      {state.status === 'error' && <p className="error-message">{state.message}</p>}
    </div>
  );
}
