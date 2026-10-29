import { TripSettle } from '../trips.service';

/** The card badge: its words (F6.R20, the words carry the meaning) and its style. */
export interface SettleBadge {
  text: string;
  tone: 'toSettle' | 'settled';
}

/**
 * Card badge for a trip's settle status (F6.R19): "{n} payments to settle" or "1 payment to settle" in the amber
 * style, "Settled up" in the green style, and no badge for none or when the list gave no status.
 */
export function settleBadge(settle: TripSettle | undefined): SettleBadge | null {
  if (!settle || settle.status === 'none') {
    return null;
  }
  if (settle.status === 'settled') {
    return { text: 'Settled up', tone: 'settled' };
  }
  const count = settle.paymentsToSettle;
  return {
    text: count === 1 ? '1 payment to settle' : `${count} payments to settle`,
    tone: 'toSettle',
  };
}
