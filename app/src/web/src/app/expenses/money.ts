/**
 * Formats whole cents for display only (F4.R13). The amount stays an integer
 * everywhere else; this never does float arithmetic. 1050 becomes "10.50" and 0 becomes "0.00".
 */
export function formatCents(cents: number): string {
  const sign = cents < 0 ? '-' : '';
  const absolute = Math.abs(cents);
  const whole = Math.floor(absolute / 100);
  const fraction = absolute % 100;
  return `${sign}${whole}.${String(fraction).padStart(2, '0')}`;
}
