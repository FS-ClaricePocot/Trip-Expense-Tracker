/**
 * The count and the right word for it: "1 payment", "0 payments", "2 payments" (F6 spec, Plural forms).
 * Only exact 1 takes the singular, so 0 and every other count take the plural.
 */
export function pluralize(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`;
}
