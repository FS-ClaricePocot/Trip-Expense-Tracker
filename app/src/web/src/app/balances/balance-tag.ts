import { formatCentsGrouped } from '../expenses/money';
import { BalanceStatus } from './balances.service';

/**
 * The tag words (F5.R6, mockup final): "is owed 50.00", "owes 10.00", "settled up".
 * The sign is never shown; the absolute amount is formatted with two decimals and separators.
 * A zero balance is "settled up" with no amount, even if the status says otherwise.
 */
export function balanceTagText(status: BalanceStatus, balanceCents: number): string {
  if (status === 'settled' || balanceCents === 0) {
    return 'settled up';
  }
  const amount = formatCentsGrouped(Math.abs(balanceCents));
  return status === 'owed' ? `is owed ${amount}` : `owes ${amount}`;
}
