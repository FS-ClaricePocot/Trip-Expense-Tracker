import { Dialog } from '@angular/cdk/dialog';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { formatCentsGrouped } from '../../expenses/money';
import {
  DeletePaymentDialogComponent,
  DeletePaymentDialogData,
  DeletePaymentResult,
} from '../delete-payment-dialog/delete-payment-dialog.component';
import { formatPaymentDate } from '../date-format';
import { pluralize } from '../pluralize';
import { RecordedPayment, SettleUpService } from '../settle-up.service';

/** Shown after a payment is deleted (F6.R8). */
export const PAYMENT_DELETED_MESSAGE = 'Payment deleted.';
/** Shown when the payment was already deleted, or is not on this trip (F6.R15). */
export const PAYMENT_MISSING_MESSAGE = 'This payment no longer exists.';
/** The empty state (States table). */
export const NO_PAYMENTS_MESSAGE = 'No payments yet. Payments you mark as paid will appear here.';

/**
 * The Payments made list (F6.R7, F6.R8, F6.R9). Shows the payments the Settle up view passes in, newest
 * first as the API returns them, each with a delete control. Payments cannot be edited (F6.R9).
 * The list does not fetch: the Settle up view owns the settle-up data and reloads it on every change.
 */
@Component({
  selector: 'app-payments-made-list',
  templateUrl: './payments-made-list.component.html',
  styleUrl: './payments-made-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentsMadeListComponent implements OnInit {
  private readonly settleUpService = inject(SettleUpService);
  private readonly dialog = inject(Dialog);
  private readonly destroyRef = inject(DestroyRef);

  /** The trip the payments belong to. */
  readonly tripId = input.required<string>();
  /** The trip's recorded payments, newest first (F6.R7), from the settle-up response. */
  readonly payments = input.required<RecordedPayment[]>();
  /** Emitted when a delete finds the payment already gone, so the view reloads the settle-up data (F6.R15). */
  readonly refresh = output<void>();

  /** "Payment deleted." or "This payment no longer exists." after a delete attempt. */
  protected readonly notice = signal<string | null>(null);
  protected readonly emptyMessage = NO_PAYMENTS_MESSAGE;
  protected readonly format = formatCentsGrouped;

  /** "1 payment" for one, otherwise "{n} payments" (F6.T7 plural rule). */
  protected readonly countText = computed(() => pluralize(this.payments().length, 'payment', 'payments'));

  ngOnInit(): void {
    // A payment recorded or deleted on this trip clears this list's notice; the view reloads the rows (F6.AC5, F6.AC6).
    this.settleUpService.paymentsChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.notice.set(null));
  }

  /** The day the payment was marked paid, in the browser's local time zone (F6 spec, Edge cases). */
  protected markedDate(payment: RecordedPayment): string {
    return formatPaymentDate(payment.markedPaidAt);
  }

  /** The accessible name of the delete control, so a screen reader user can tell the rows apart (Accessibility). */
  protected deleteLabel(payment: RecordedPayment): string {
    return `Delete ${payment.payerName} paid ${payment.payeeName} ${this.format(payment.amountCents)}`;
  }

  /** F6.R8: opens the confirmation for one payment. The dialog returns focus to the control on close. */
  protected openDelete(payment: RecordedPayment): void {
    this.notice.set(null);
    const data: DeletePaymentDialogData = { tripId: this.tripId(), payment };
    const ref = this.dialog.open<DeletePaymentResult | undefined>(DeletePaymentDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'delete-payment-title',
      ariaDescribedBy: 'delete-payment-body',
      // Initial focus goes to Cancel, since the action is destructive (F6.R8).
      autoFocus: '.delete-payment__cancel',
    });
    ref.closed.subscribe((result) => this.onDeleteClosed(result));
  }

  private onDeleteClosed(result: DeletePaymentResult | undefined): void {
    if (result === 'deleted') {
      // The service has already emitted the change, so the view is reloading the rows.
      this.notice.set(PAYMENT_DELETED_MESSAGE);
    } else if (result === 'missing') {
      // Already gone (for example deleted in another tab). The view reloads so the row disappears.
      this.notice.set(PAYMENT_MISSING_MESSAGE);
      this.refresh.emit();
    }
  }
}
