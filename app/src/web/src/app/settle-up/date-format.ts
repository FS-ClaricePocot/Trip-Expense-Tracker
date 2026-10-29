/**
 * The day a payment was marked paid, as "20 Oct 2026": the full day, month and year (F6 spec, Edge cases).
 * The UTC timestamp from the API is converted for display only. Without a time zone the browser's local
 * zone is used; tests pass a fixed zone so they do not depend on the machine.
 */
export function formatPaymentDate(utcTimestamp: string, timeZone?: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone,
  }).format(new Date(utcTimestamp));
}
