import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { BalancesPanelComponent } from '../../balances/balances-panel/balances-panel.component';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { SettleUp, SettleUpService } from '../settle-up.service';
import { SummaryTilesSettleComponent } from '../summary-tiles-settle/summary-tiles-settle.component';

/**
 * The Settle up view (F6.R12, F6.R13): the three tiles, then the Balances panel from F5, unchanged.
 * The view loads its figures when it opens; the Expenses view is a separate view, so it reloads on return.
 */
@Component({
  selector: 'app-settle-up-view',
  imports: [SummaryTilesSettleComponent, BalancesPanelComponent],
  templateUrl: './settle-up-view.component.html',
  styleUrl: './settle-up-view.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettleUpViewComponent implements OnInit {
  private readonly settleUpService = inject(SettleUpService);

  /** The trip whose settle-up figures are shown. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F6.R15). */
  readonly tripGone = output<void>();

  protected readonly settleUp = signal<SettleUp | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;

  async ngOnInit(): Promise<void> {
    try {
      this.settleUp.set(await this.settleUpService.get(this.tripId()));
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
