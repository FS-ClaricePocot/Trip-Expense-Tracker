import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { formatCentsGrouped } from '../../expenses/money';
import { pluralize } from '../pluralize';
import { SettleUp } from '../settle-up.service';

/**
 * The three Settle up tiles (F6.R12), in order: Paid back so far with its progress bar,
 * Left to settle, and Total spent. The tiles are headings with the amounts in their text.
 */
@Component({
  selector: 'app-summary-tiles-settle',
  templateUrl: './summary-tiles-settle.component.html',
  styleUrl: './summary-tiles-settle.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SummaryTilesSettleComponent {
  /** The settle-up figures for the trip. */
  readonly summary = input.required<SettleUp>();

  protected readonly format = formatCentsGrouped;

  /** "in 1 payment", "in 0 payments", "in 2 payments" (F6.R12, F5.R8). */
  protected paymentsNote(count: number): string {
    return `in ${pluralize(count, 'payment', 'payments')}`;
  }

  /** "across 1 expense", "across 3 expenses" (F6.R12). */
  protected expensesNote(count: number): string {
    return `across ${pluralize(count, 'expense', 'expenses')}`;
  }
}
