/**
 * Card text for a trip's expense count (F4.R17): "no expenses yet", "1 expense" or "{n} expenses".
 * Pure, so the wording is unit-tested without rendering a card.
 */
export function tripExpenseText(count: number): string {
  if (count === 0) {
    return 'no expenses yet';
  }
  return count === 1 ? '1 expense' : `${count} expenses`;
}
