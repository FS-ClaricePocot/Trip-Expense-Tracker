/**
 * Card text for a trip's people count (F3.R13): "No people yet", "1 person" or "{n} people".
 * Pure, so the wording is unit-tested without rendering a card.
 */
export function tripPeopleText(count: number): string {
  if (count === 0) {
    return 'No people yet';
  }
  return count === 1 ? '1 person' : `${count} people`;
}
