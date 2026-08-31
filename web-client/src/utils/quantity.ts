// Whether a unit is fractional-friendly or decimal-only comes from the server
// (see useUnits) -- this module only knows how to parse/format a quantity once
// told which style applies, it doesn't hold its own copy of the unit catalog.

const MIXED_NUMBER_RE = /^(\d+)?\s*(\d+)\s*\/\s*(\d+)$/;

// Common cooking fractions, checked in order so "1/2" reduces to itself rather
// than e.g. 4/8.
const COMMON_FRACTIONS: [numerator: number, denominator: number][] = [
  [1, 8],
  [1, 4],
  [1, 3],
  [3, 8],
  [1, 2],
  [5, 8],
  [2, 3],
  [3, 4],
  [7, 8],
];

export function parseQuantityInput(input: string, fractionalFriendly: boolean): number | null {
  const trimmed = input.trim();
  if (!trimmed) return null;

  if (fractionalFriendly) {
    const mixed = MIXED_NUMBER_RE.exec(trimmed);
    if (mixed) {
      const whole = mixed[1] ? Number(mixed[1]) : 0;
      const denominator = Number(mixed[3]);
      if (denominator === 0) return null;
      return whole + Number(mixed[2]) / denominator;
    }
  }

  const value = Number(trimmed);
  return Number.isFinite(value) ? value : null;
}

export function formatQuantity(value: number, fractionalFriendly: boolean): string {
  if (!fractionalFriendly) {
    // Matches recipe_ingredients.quantity's own NUMERIC(10,2) precision -- a no-op for any
    // stored value, but keeps a scaled quantity (e.g. 1 * 5/3) from printing as a long float.
    return String(Number(value.toFixed(2)));
  }

  const whole = Math.floor(value + 1e-9);
  const remainder = value - whole;

  // Always snap to whichever candidate -- no fraction, one of the common cooking fractions, or
  // rounding up to the next whole number -- is numerically closest, rather than only matching
  // within a tight tolerance and otherwise falling back to a raw float. A scaled quantity (e.g.
  // servings scaling produces 0.9583333...) rarely lands exactly on a common fraction, but it
  // should still read as "1", not "0.9583333333333334".
  const candidates: [numerator: number, denominator: number][] = [[0, 1], ...COMMON_FRACTIONS, [1, 1]];
  let [bestNumerator, bestDenominator] = candidates[0];
  let bestDiff = Math.abs(remainder);
  for (const [numerator, denominator] of candidates) {
    const diff = Math.abs(remainder - numerator / denominator);
    if (diff < bestDiff) {
      [bestNumerator, bestDenominator] = [numerator, denominator];
      bestDiff = diff;
    }
  }

  if (bestNumerator === 0) return String(whole);
  if (bestNumerator === bestDenominator) return String(whole + 1);
  return whole > 0 ? `${whole} ${bestNumerator}/${bestDenominator}` : `${bestNumerator}/${bestDenominator}`;
}
