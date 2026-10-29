import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { formatCentsGrouped } from '../../expenses/money';
import { SettleUpService, SuggestedPayment } from '../settle-up.service';

/**
 * The Suggested payments panel (F6.R1, F6.R14). Lists the suggestions in the order the API returns them,
 * each with a Mark as paid button. With no suggestions it shows the settled state.
 */
@Component({
  selector: 'app-suggested-payments-panel',
  templateUrl: './suggested-payments-panel.component.html',
  styleUrl: './suggested-payments-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuggestedPaymentsPanelComponent implements OnInit {
  private readonly settleUpService = inject(SettleUpService);

  /** The trip whose suggested payments are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F6.R15). */
  readonly tripGone = output<void>();
  /** Emitted when a Mark as paid button is pressed; the confirmation dialog is wired in a later task. */
  readonly markPaid = output<SuggestedPayment>();

  protected readonly suggestions = signal<SuggestedPayment[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;
  protected readonly format = formatCentsGrouped;

  async ngOnInit(): Promise<void> {
    try {
      const settleUp = await this.settleUpService.get(this.tripId());
      this.suggestions.set(settleUp.suggestions);
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

  /** The accessible name of the button, so a screen reader user can tell the rows apart (Accessibility). */
  protected markLabel(suggestion: SuggestedPayment): string {
    return `Mark ${suggestion.payerName} pays ${suggestion.payeeName} ${this.format(suggestion.amountCents)} as paid`;
  }
}
