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
import { ExpensesService } from '../../expenses/expenses.service';
import { formatCentsGrouped } from '../../expenses/money';
import { BalancesService, TripSummary } from '../balances.service';

/**
 * The two summary tiles above the trip panels (F5.R8): Total spent, then Left to settle.
 * The tiles are headings with the amounts in text. The "Biggest payer" tile is not built in F5.
 */
@Component({
  selector: 'app-summary-tiles',
  templateUrl: './summary-tiles.component.html',
  styleUrl: './summary-tiles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SummaryTilesComponent implements OnInit {
  private readonly balancesService = inject(BalancesService);
  private readonly expensesService = inject(ExpensesService);
  private readonly destroyRef = inject(DestroyRef);

  /** The trip whose summary is shown. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F5.R12). */
  readonly tripGone = output<void>();

  protected readonly summary = signal<TripSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;
  protected readonly format = formatCentsGrouped;

  async ngOnInit(): Promise<void> {
    // F5.R7: an expense added or deleted on this trip changes the totals, so reload them.
    this.expensesService.expensesChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => void this.load());
    await this.load();
  }

  private async load(): Promise<void> {
    try {
      this.summary.set(await this.balancesService.summary(this.tripId()));
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
