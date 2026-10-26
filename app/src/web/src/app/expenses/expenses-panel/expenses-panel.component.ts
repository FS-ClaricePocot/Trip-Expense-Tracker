import { Dialog } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal, computed } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { AddExpenseFormComponent } from '../add-expense-form/add-expense-form.component';
import {
  DeleteExpenseDialogComponent,
  DeleteExpenseDialogData,
  DeleteExpenseResult,
} from '../delete-expense-dialog/delete-expense-dialog.component';
import { Expense, ExpenseList, ExpensesService } from '../expenses.service';
import { formatCents } from '../money';
import { SharesDialogComponent, SharesDialogData } from '../shares-dialog/shares-dialog.component';

/**
 * The Expenses panel on the trip page (F4.R8, F4.R10 to F4.R13). Lists the expenses
 * newest first, with the count and the total, and the add form below them (F4.T8).
 * Each row has a trash control (F4.R9) and opens a shares dialog when selected (F4.R10a).
 * It has no edit control (F4.R12), and the generic icon is shown on every row until the
 * keyword rule is decided (F4.R11 interim).
 */
@Component({
  selector: 'app-expenses-panel',
  imports: [AddExpenseFormComponent],
  templateUrl: './expenses-panel.component.html',
  styleUrl: './expenses-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpensesPanelComponent implements OnInit {
  private readonly expensesService = inject(ExpensesService);
  private readonly dialog = inject(Dialog);

  /** The trip whose expenses are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message. */
  readonly tripGone = output<void>();

  protected readonly list = signal<ExpenseList | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  /** Status text after a delete: "{description} was deleted." or "This expense no longer exists." (F4.R9). */
  protected readonly notice = signal('');
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

  /** F4.R8: a saved expense goes to the top, and the count and total move with it. */
  protected onAdded(expense: Expense): void {
    this.list.update((current) =>
      current === null
        ? current
        : {
            total: current.total + expense.amountCents,
            count: current.count + 1,
            expenses: [expense, ...current.expenses],
          },
    );
  }

  /** F4.R10: "{payer} paid · split {N} ways". Shares are not shown, because leftover cents can differ. */
  protected paidText(expense: Expense): string {
    const ways = expense.participantCount === 1 ? 'way' : 'ways';
    return `${expense.payer.name} paid · split ${expense.participantCount} ${ways}`;
  }

  /** F4.R9: opens the confirmation. Focus returns to the trash control when it closes. */
  protected openDelete(expense: Expense): void {
    const data: DeleteExpenseDialogData = {
      tripId: this.tripId(),
      expenseId: expense.id,
      description: expense.description,
    };
    const ref = this.dialog.open<DeleteExpenseResult>(DeleteExpenseDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'delete-expense-title',
      ariaDescribedBy: 'delete-expense-body',
      // Initial focus goes to Cancel, since the action is destructive (F4.R9).
      autoFocus: '.delete-expense__cancel',
    });
    ref.closed.subscribe((result) => {
      if (result === 'deleted') {
        this.removeExpense(expense, `${expense.description} was deleted.`);
      } else if (result === 'missing') {
        // The expense is already gone on the server, so it leaves the list and the total too.
        this.removeExpense(expense, 'This expense no longer exists.');
      }
    });
  }

  /** F4.R10a: selecting a row opens its shares. Close returns focus to the row. */
  protected openShares(expense: Expense): void {
    const data: SharesDialogData = {
      description: expense.description,
      amountCents: expense.amountCents,
      participants: expense.participants,
    };
    this.dialog.open<void>(SharesDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'shares-title',
      autoFocus: '.shares__close',
    });
  }

  /** F4.R9: the row leaves the list, and the total and count move with it. */
  private removeExpense(expense: Expense, message: string): void {
    this.list.update((current) => {
      if (current === null || !current.expenses.some((item) => item.id === expense.id)) {
        return current;
      }
      return {
        total: current.total - expense.amountCents,
        count: current.count - 1,
        expenses: current.expenses.filter((item) => item.id !== expense.id),
      };
    });
    this.notice.set(message);
  }
}
