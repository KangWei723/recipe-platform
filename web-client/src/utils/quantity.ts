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

const FRACTION_TOLERANCE = 0.02;

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
    return String(value);
  }

  const whole = Math.floor(value + 1e-9);
  const remainder = value - whole;

  if (remainder < FRACTION_TOLERANCE) {
    return String(whole);
  }

  for (const [numerator, denominator] of COMMON_FRACTIONS) {
    if (Math.abs(remainder - numerator / denominator) < FRACTION_TOLERANCE) {
      return whole > 0 ? `${whole} ${numerator}/${denominator}` : `${numerator}/${denominator}`;
    }
  }

  return String(value);
}
