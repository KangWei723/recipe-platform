import { useQuery } from 'urql';
import { UNITS_QUERY } from './queries';
import type { MeasurementUnit } from './types';

export function useUnits() {
  const [{ data, fetching }] = useQuery<{ units: MeasurementUnit[] }>({ query: UNITS_QUERY });
  const units = data?.units ?? [];

  function isFractionalFriendly(unitCode: string): boolean {
    return units.find((u) => u.code === unitCode)?.isFractionalFriendly ?? false;
  }

  return { units, fetching, isFractionalFriendly };
}
