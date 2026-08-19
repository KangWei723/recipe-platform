import { useState } from 'react';
import { useClient } from 'urql';
import { NEARBY_STORES_QUERY } from '../graphql/queries';
import type { StoreOffer } from '../graphql/types';
import { distanceMiles } from '../utils/distance';

function mapsUrl(store: StoreOffer): string | null {
  if (store.placeId) {
    return `https://www.google.com/maps/place/?q=place_id:${store.placeId}`;
  }
  if (store.lat !== null && store.lng !== null) {
    return `https://www.google.com/maps/search/?api=1&query=${store.lat},${store.lng}`;
  }
  return null;
}

type LookupState =
  | { status: 'idle' }
  | { status: 'locating' }
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'done'; stores: StoreOffer[]; userLat: number; userLng: number };

export function NearbyStoresFinder({ ingredientName }: { ingredientName: string }) {
  const client = useClient();
  const [state, setState] = useState<LookupState>({ status: 'idle' });

  function handleFindStores() {
    if (!('geolocation' in navigator)) {
      setState({ status: 'error', message: 'Geolocation is not supported by this browser.' });
      return;
    }

    setState({ status: 'locating' });
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const { latitude, longitude } = position.coords;
        setState({ status: 'loading' });
        client
          .query<{ nearbyStores: StoreOffer[] }>(NEARBY_STORES_QUERY, {
            ingredientName,
            lat: latitude,
            lng: longitude,
          })
          .toPromise()
          .then((result) => {
            if (result.error || !result.data) {
              setState({ status: 'error', message: 'Could not look up nearby stores right now.' });
              return;
            }
            setState({
              status: 'done',
              stores: result.data.nearbyStores,
              userLat: latitude,
              userLng: longitude,
            });
          });
      },
      (geoError) => {
        const message =
          geoError.code === geoError.PERMISSION_DENIED
            ? 'Location permission was denied, so nearby stores can’t be looked up.'
            : 'Could not determine your location.';
        setState({ status: 'error', message });
      },
      { timeout: 10_000 },
    );
  }

  if (state.status === 'locating') {
    return <p className="nearby-stores-status">Getting your location...</p>;
  }

  if (state.status === 'loading') {
    return <p className="nearby-stores-status">Searching nearby stores...</p>;
  }

  if (state.status === 'done') {
    if (state.stores.length === 0) {
      return (
        <div>
          <p className="nearby-stores-status">No nearby stores found for this ingredient.</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={handleFindStores}>
            Search again
          </button>
        </div>
      );
    }

    return (
      <div>
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
        <button type="button" onClick={handleFindStores}>
          Search again
        </button>
      </div>
    );
  }

  return (
    <div>
      <button type="button" className="btn btn-outline btn-sm" onClick={handleFindStores}>
        Find nearby stores
      </button>
      {state.status === 'error' && <p className="error-message">{state.message}</p>}
    </div>
  );
}
