/** Balances up to this many cents (50.00) reach 25% of the track, halfway from the centre to the end (F5.R6). */
const FULL_SCALE_CENTS = 5000;
/** The bar's length on the scale at 50.00, as a percent of the whole track. */
const FULL_SCALE_PERCENT = 25;
/** The end of the track on one side, as a percent of the whole track. Larger balances are clipped here. */
const TRACK_END_PERCENT = 50;

/**
 * Bar length for CSS only (F5.R6, F5.AC17). Takes integer cents and returns the length as a percent
 * of the whole track, on the side the sign gives. The magnitude is returned, so -5000 gives 25.
 * The result is never stored or summed.
 */
export function barWidthPercent(balanceCents: number): number {
  const magnitude = Math.abs(balanceCents);
  if (magnitude === 0) {
    return 0;
  }
  if (magnitude > FULL_SCALE_CENTS) {
    return TRACK_END_PERCENT;
  }
  return (magnitude * FULL_SCALE_PERCENT) / FULL_SCALE_CENTS;
}
