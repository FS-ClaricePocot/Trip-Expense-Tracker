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
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { formatCentsGrouped } from '../../expenses/money';
import { SettleUpService, SuggestedPayment } from '../settle-up.service';

/** Shown when the server refuses a payment because the suggestions changed (F6.AC9). */
export const SUGGESTIONS_CHANGED_MESSAGE = 'The suggested payments have changed. Review them and try again.';

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
  private readonly destroyRef = inject(DestroyRef);

  /** The trip whose suggested payments are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F6.R15). */
  readonly tripGone = output<void>();
  /** Emitted when a Mark as paid button is pressed; the settle-up view opens the confirmation dialog. */
  readonly markPaid = output<SuggestedPayment>();

  protected readonly suggestions = signal<SuggestedPayment[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  /** A message for this panel, such as the stale-suggestion message (F6.AC9). */
  protected readonly notice = signal<string | null>(null);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;
  protected readonly format = formatCentsGrouped;

  async ngOnInit(): Promise<void> {
    // F6.AC5: a payment recorded or deleted on this trip changes the suggestions, so reload them.
    this.settleUpService.paymentsChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.notice.set(null);
        void this.reload();
      });
    await this.reload();
  }

  /**
   * F6.AC9: the server refused a Mark as paid because the suggestions changed. Shows the message here
   * and reloads the suggestions. The settle-up view calls this after its dialog closes.
   */
  async showSuggestionsChanged(): Promise<void> {
    this.notice.set(SUGGESTIONS_CHANGED_MESSAGE);
    await this.reload();
  }

  /** Reloads the suggestions. A 404 means the trip is gone, so the trip page shows its message (F6.R15). */
  async reload(): Promise<void> {
    try {
      const settleUp = await this.settleUpService.get(this.tripId());
      this.failed.set(false);
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
