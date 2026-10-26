import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal, computed } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { Expense, ExpenseList, ExpensesService } from '../expenses.service';
import { formatCents } from '../money';

/**
 * The Expenses panel on the trip page (F4.R8, F4.R10 to F4.R13). Lists the expenses
 * newest first, with the count and the total. It has no edit control (F4.R12), and the
 * generic icon is shown on every row until the keyword rule is decided (F4.R11 interim).
 */
@Component({
  selector: 'app-expenses-panel',
  templateUrl: './expenses-panel.component.html',
  styleUrl: './expenses-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpensesPanelComponent implements OnInit {
  private readonly expensesService = inject(ExpensesService);

  /** The trip whose expenses are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message. */
  readonly tripGone = output<void>();

  protected readonly list = signal<ExpenseList | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;
  protected readonly formatCents = formatCents;

  /** "1 expense" for one, otherwise "{n} expenses" (the same wording as the delete dialog). */
  protected readonly countText = computed(() => {
    const count = this.list()?.count ?? 0;
    return count === 1 ? '1 expense' : `${count} expenses`;
  });

  async ngOnInit(): Promise<void> {
    try {
      this.list.set(await this.expensesService.list(this.tripId()));
    } catch (error: unknown) {
      // A 401 is sent to log-in by the API interceptor.
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.tripGone.emit();
      } else {
        this.failed.set(true);
      }
    } finally {
      this.loading.set(false);
    }
  }

  /** F4.R10: "{payer} paid · split {N} ways". Shares are not shown, because leftover cents can differ. */
  protected paidText(expense: Expense): string {
    const ways = expense.participantCount === 1 ? 'way' : 'ways';
    return `${expense.payer.name} paid · split ${expense.participantCount} ${ways}`;
  }
}
