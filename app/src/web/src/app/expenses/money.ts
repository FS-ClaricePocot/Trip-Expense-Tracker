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

/**
 * Like formatCents, with a thousands separator on the whole units (F5.R9): 184250 becomes "1,842.50".
 * The sign is kept only for negative input, so balance tags pass the absolute value.
 */
export function formatCentsGrouped(cents: number): string {
  const plain = formatCents(cents);
  const negative = plain.startsWith('-');
  const [whole, fraction] = (negative ? plain.slice(1) : plain).split('.');
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return `${negative ? '-' : ''}${grouped}.${fraction}`;
}
