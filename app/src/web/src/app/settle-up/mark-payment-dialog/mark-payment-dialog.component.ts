import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { formatCentsGrouped } from '../../expenses/money';
import { SettleUpService, SuggestedPayment } from '../settle-up.service';

/** What the settle-up view passes in: the trip and the suggestion the user saw (F6.R18). */
export interface MarkPaymentDialogData {
  tripId: string;
  suggestion: SuggestedPayment;
}

/**
 * How the dialog closes, when it is done with the request:
 * - `recorded`: the server recorded the payment (201). The view shows "Payment recorded."
 * - `stale`: the suggestions changed, so nothing was recorded (409, F6.AC9). The view shows the message in the panel.
 * - `missing`: the trip is gone (404), so the view reloads and the trip page shows its own message.
 * Cancel closes without a result and sends nothing (F6.AC4).
 */
export type MarkPaymentResult = 'recorded' | 'stale' | 'missing';

/**
 * Confirmation dialog for marking a suggested payment as paid (F6.R5, F6.R6). Layout follows the
 * F2 delete dialog. Initial focus is on Cancel, set by the view.
 */
@Component({
  selector: 'app-mark-payment-dialog',
  templateUrl: './mark-payment-dialog.component.html',
  styleUrl: './mark-payment-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarkPaymentDialogComponent {
  private readonly settleUpService = inject(SettleUpService);
  private readonly dialogRef = inject<DialogRef<MarkPaymentResult | undefined>>(DialogRef);

  protected readonly data = inject<MarkPaymentDialogData>(DIALOG_DATA);
  protected readonly amount = formatCentsGrouped(this.data.suggestion.amountCents);
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
    const { suggestion } = this.data;
    try {
      await this.settleUpService.markPaid(this.data.tripId, {
        payerPersonId: suggestion.payerPersonId,
        payeePersonId: suggestion.payeePersonId,
        amountCents: suggestion.amountCents,
      });
      this.dialogRef.close('recorded');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.dialogRef.close('stale');
        return;
      }
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
