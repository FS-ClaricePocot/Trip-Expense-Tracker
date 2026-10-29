import { Dialog } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { BalancesPanelComponent } from '../../balances/balances-panel/balances-panel.component';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import {
  MarkPaymentDialogComponent,
  MarkPaymentDialogData,
  MarkPaymentResult,
} from '../mark-payment-dialog/mark-payment-dialog.component';
import { PaymentsMadeListComponent } from '../payments-made-list/payments-made-list.component';
import { SettleUp, SettleUpService, SuggestedPayment } from '../settle-up.service';
import { SuggestedPaymentsPanelComponent } from '../suggested-payments-panel/suggested-payments-panel.component';
import { SummaryTilesSettleComponent } from '../summary-tiles-settle/summary-tiles-settle.component';

/** Shown in the status region after a payment is recorded (F6.AC5). */
export const PAYMENT_RECORDED_MESSAGE = 'Payment recorded.';

/**
 * The Settle up view (F6.R12, F6.R13): the three tiles, the Suggested payments panel, the Payments made
 * list, then the Balances panel from F5, unchanged. Mark as paid opens its confirmation here. The view loads its figures when it
 * opens; the Expenses view is a separate view, so it reloads on return.
 */
@Component({
  selector: 'app-settle-up-view',
  imports: [
    SummaryTilesSettleComponent,
    SuggestedPaymentsPanelComponent,
    PaymentsMadeListComponent,
    BalancesPanelComponent,
  ],
  templateUrl: './settle-up-view.component.html',
  styleUrl: './settle-up-view.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettleUpViewComponent implements OnInit {
  private readonly settleUpService = inject(SettleUpService);
  private readonly dialog = inject(Dialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly suggestedPanel = viewChild.required(SuggestedPaymentsPanelComponent);

  /** The trip whose settle-up figures are shown. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F6.R15). */
  readonly tripGone = output<void>();

  protected readonly settleUp = signal<SettleUp | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  /** "Payment recorded." after a successful Mark as paid (F6.AC5). */
  protected readonly notice = signal<string | null>(null);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;

  async ngOnInit(): Promise<void> {
    // F6.AC5: a payment recorded or deleted on this trip changes the tiles, so reload them.
    this.settleUpService.paymentsChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.notice.set(null);
        void this.load();
      });
    await this.load();
  }

  /** F6.R5: Mark as paid on a suggestion opens the confirmation. Focus returns to the button when it closes. */
  protected openMark(suggestion: SuggestedPayment): void {
    const data: MarkPaymentDialogData = { tripId: this.tripId(), suggestion };
    const ref = this.dialog.open<MarkPaymentResult>(MarkPaymentDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'mark-payment-title',
      ariaDescribedBy: 'mark-payment-body',
      // Initial focus goes to Cancel, since the payment is recorded on confirm (F6.R5).
      autoFocus: '.mark-payment__cancel',
    });
    ref.closed.subscribe((result) => void this.onMarkClosed(result));
  }

  private async onMarkClosed(result: MarkPaymentResult | undefined): Promise<void> {
    if (result === 'recorded') {
      // The service has already reloaded the tiles and suggestions; the notice is set after that reload starts.
      this.notice.set(PAYMENT_RECORDED_MESSAGE);
    } else if (result === 'stale') {
      // F6.AC9: nothing was recorded. The message shows in the panel, and the suggestions reload.
      await this.suggestedPanel().showSuggestionsChanged();
    } else if (result === 'missing') {
      // The trip is gone. Both reloads hand over to the trip page on their 404 (F6.R15).
      await this.suggestedPanel().reload();
      await this.load();
    }
  }

  /** Reloads the settle-up figures. The Payments made list calls it when a delete finds the payment gone. */
  protected async load(): Promise<void> {
    try {
      this.settleUp.set(await this.settleUpService.get(this.tripId()));
      this.failed.set(false);
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
}
