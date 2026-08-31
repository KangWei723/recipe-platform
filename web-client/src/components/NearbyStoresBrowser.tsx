import { useState } from 'react';
import { useClient } from 'urql';
import { GENERAL_STORES_QUERY } from '../graphql/queries';
import type { StoreOffer } from '../graphql/types';
import { distanceMiles } from '../utils/distance';
import type { GeolocationResult } from '../utils/useGeolocation';
import { Button } from './ui/button';

function mapsUrl(store: StoreOffer): string | null {
  if (store.placeId) {
    return `https://www.google.com/maps/place/?q=place_id:${store.placeId}`;
  }
  if (store.lat !== null && store.lng !== null) {
    return `https://www.google.com/maps/search/?api=1&query=${store.lat},${store.lng}`;
  }
  return null;
}

type BrowseState =
  | { status: 'idle' }
  | { status: 'locating' }
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'done'; stores: StoreOffer[]; userLat: number; userLng: number };

// Consolidated, once-per-page general lookup -- unlike ConfirmedStoreLookup, this isn't scoped
// to (or repeated per) any one ingredient, since a general locator like Google Places returns the
// same "nearby grocery stores" list regardless of which ingredient asked. Results here are
// explicitly not confirmed to carry any particular ingredient.
export function NearbyStoresBrowser({ getLocation }: { getLocation: () => Promise<GeolocationResult> }) {
  const client = useClient();
  const [state, setState] = useState<BrowseState>({ status: 'idle' });

  async function handleBrowse() {
    setState({ status: 'locating' });
    const location = await getLocation();
    if (!location.ok) {
      setState({ status: 'error', message: location.message });
      return;
    }

    const { lat, lng } = location.coords;
    setState({ status: 'loading' });
    const result = await client
      .query<{ nearbyStoresGeneral: StoreOffer[] }>(GENERAL_STORES_QUERY, { lat, lng })
      .toPromise();

    if (result.error || !result.data) {
      setState({ status: 'error', message: 'Could not look up nearby stores right now.' });
      return;
    }
    setState({ status: 'done', stores: result.data.nearbyStoresGeneral, userLat: lat, userLng: lng });
  }

  return (
    <div className="general-stores-section">
      <p className="panel-subtitle">
        General nearby grocery stores — not confirmed to carry any specific missing ingredient.
      </p>

      {state.status === 'locating' && <p className="nearby-stores-status">Getting your location...</p>}
      {state.status === 'loading' && <p className="nearby-stores-status">Searching nearby stores...</p>}

      {state.status === 'done' && state.stores.length === 0 && (
        <p className="nearby-stores-status">No nearby stores found.</p>
      )}

      {state.status === 'done' && state.stores.length > 0 && (
        <ul className="nearby-stores-list">
          {state.stores.map((store, index) => {
            const distance =
              store.lat !== null && store.lng !== null
                ? distanceMiles(state.userLat, state.userLng, store.lat, store.lng)
                : null;
            const url = mapsUrl(store);
            const label = (
              <>
                {store.storeName}
                {distance !== null ? ` — ${distance.toFixed(1)} mi` : ''}
                {store.price !== null ? ` — ${store.currency ?? '$'}${store.price.toFixed(2)}` : ''}
                {store.isSimulated ? ' (estimated)' : ''}
              </>
            );
            return (
              <li key={`${store.providerName}-${store.storeName}-${index}`}>
                {url ? (
                  <a href={url} target="_blank" rel="noopener noreferrer">
                    {label}
                  </a>
                ) : (
                  label
                )}
              </li>
            );
          })}
        </ul>
      )}

      {state.status === 'error' && <p className="error-message">{state.message}</p>}

      <Button type="button" variant="outline" size="sm" onClick={handleBrowse}>
        {state.status === 'done' ? 'Search again' : 'Browse other nearby stores'}
      </Button>
    </div>
  );
}
