import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { formatCentsGrouped } from '../../expenses/money';
import { RecordedPayment, SettleUpService } from '../settle-up.service';

/** What the Payments made list passes in: the trip and the payment to delete (F6.R8). */
export interface DeletePaymentDialogData {
  tripId: string;
  payment: RecordedPayment;
}

/**
 * How the dialog closes, when it is done with the request:
 * - `deleted`: the server removed the payment (204). The lists and tiles reload.
 * - `missing`: the payment or the trip is gone (404), so the list shows "This payment no longer exists."
 * Cancel closes without a result and sends nothing, so the payment stays (F6.AC6).
 */
export type DeletePaymentResult = 'deleted' | 'missing';

/**
 * Confirmation dialog for deleting a recorded payment (F6.R8). Layout follows the F2 delete dialog.
 * Initial focus is on Cancel, set by the opener (the action is destructive).
 */
@Component({
  selector: 'app-delete-payment-dialog',
  templateUrl: './delete-payment-dialog.component.html',
  styleUrl: './delete-payment-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeletePaymentDialogComponent {
  private readonly settleUpService = inject(SettleUpService);
  private readonly dialogRef = inject<DialogRef<DeletePaymentResult | undefined>>(DialogRef);

  protected readonly data = inject<DeletePaymentDialogData>(DIALOG_DATA);
  protected readonly amount = formatCentsGrouped(this.data.payment.amountCents);
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
      await this.settleUpService.deletePayment(this.data.tripId, this.data.payment.id);
      this.dialogRef.close('deleted');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.dialogRef.close('missing');
        return;
      }
      // A 401 is sent to log-in by the API interceptor.
      this.errorMessage.set(GENERIC_ERROR_MESSAGE);
    } finally {
      this.submitting.set(false);
    }
  }
}
