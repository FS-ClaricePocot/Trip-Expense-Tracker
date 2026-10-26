import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { ExpensesService } from '../expenses.service';

/** What the Expenses panel passes in (F4.R9). */
export interface DeleteExpenseDialogData {
  tripId: string;
  expenseId: string;
  description: string;
}

/**
 * How the dialog closed. `deleted` after a 204; `missing` after a 404, so the panel can show
 * "This expense no longer exists." (States table). Cancel closes with no result.
 */
export type DeleteExpenseResult = 'deleted' | 'missing';

/**
 * Confirmation dialog for deleting an expense (F4.R9). Cancel closes it and sends nothing.
 * Delete expense sends DELETE. Other errors stay inside the dialog. Initial focus is on Cancel.
 */
@Component({
  selector: 'app-delete-expense-dialog',
  templateUrl: './delete-expense-dialog.component.html',
  styleUrl: './delete-expense-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteExpenseDialogComponent {
  private readonly expensesService = inject(ExpensesService);
  private readonly dialogRef = inject<DialogRef<DeleteExpenseResult>>(DialogRef);

  protected readonly data = inject<DeleteExpenseDialogData>(DIALOG_DATA);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected cancel(): void {
    this.dialogRef.close();
  }

  protected async confirm(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      await this.expensesService.delete(this.data.tripId, this.data.expenseId);
      this.dialogRef.close('deleted');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.dialogRef.close('missing');
        return;
      }
      this.errorMessage.set(GENERIC_ERROR_MESSAGE);
    } finally {
      this.submitting.set(false);
    }
  }
}
