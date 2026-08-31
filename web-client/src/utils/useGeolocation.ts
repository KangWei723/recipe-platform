import { useCallback, useRef, useState } from 'react';

export interface GeolocationCoords {
  lat: number;
  lng: number;
}

export type GeolocationResult =
  | { ok: true; coords: GeolocationCoords }
  | { ok: false; message: string };

type GeolocationState =
  | { status: 'idle' }
  | { status: 'locating' }
  | { status: 'done'; coords: GeolocationCoords }
  | { status: 'error'; message: string };

// Shared across every caller on a page (e.g. the per-ingredient "Find it at Kroger" buttons and
// the once-per-page "Browse other nearby stores" button) so the browser's location permission
// prompt fires at most once per page view, not once per click. A resolved location is reused for
// the lifetime of this hook instance; concurrent callers while a request is in flight share the
// same in-flight promise instead of each triggering their own prompt.
export function useGeolocation() {
  const [state, setState] = useState<GeolocationState>({ status: 'idle' });
  const inFlightRef = useRef<Promise<GeolocationResult> | null>(null);

  const getLocation = useCallback((): Promise<GeolocationResult> => {
    if (state.status === 'done') {
      return Promise.resolve({ ok: true, coords: state.coords });
    }
    if (inFlightRef.current) {
      return inFlightRef.current;
    }
    if (!('geolocation' in navigator)) {
      const message = 'Geolocation is not supported by this browser.';
      setState({ status: 'error', message });
      return Promise.resolve({ ok: false, message });
    }

    setState({ status: 'locating' });
    const promise = new Promise<GeolocationResult>((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          const coords = { lat: position.coords.latitude, lng: position.coords.longitude };
          setState({ status: 'done', coords });
          inFlightRef.current = null;
          resolve({ ok: true, coords });
        },
        (geoError) => {
          const message =
            geoError.code === geoError.PERMISSION_DENIED
              ? 'Location permission was denied, so nearby stores can’t be looked up.'
              : 'Could not determine your location.';
          setState({ status: 'error', message });
          inFlightRef.current = null;
          resolve({ ok: false, message });
        },
        { timeout: 10_000 },
      );
    });
    inFlightRef.current = promise;
    return promise;
  }, [state]);

  return { getLocation };
}
