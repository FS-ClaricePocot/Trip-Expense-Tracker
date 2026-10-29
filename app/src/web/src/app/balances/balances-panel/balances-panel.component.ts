import { HttpErrorResponse } from '@angular/common/http';
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
import { GENERIC_ERROR_MESSAGE, problemType } from '../../core/errors/problem-mapper';
import { ExpensesService } from '../../expenses/expenses.service';
import { balanceTagText } from '../balance-tag';
import { barWidthPercent } from '../bar-scale';
import { BalanceList, BalancesService, PersonBalance } from '../balances.service';

/** The problem type the API sends when the zero-sum check fails (F5.R3, F5.AC16). */
const INCONSISTENT_TYPE = 'balances_inconsistent';
export const INCONSISTENT_MESSAGE = 'Balances could not be checked. Please try again.';

/**
 * The Balances panel on the trip page (F5.R4 to F5.R6). Lists each person in seq order with
 * a tag and a bar. It has no input control (F5.R11), and the balances are not editable.
 */
@Component({
  selector: 'app-balances-panel',
  templateUrl: './balances-panel.component.html',
  styleUrl: './balances-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BalancesPanelComponent implements OnInit {
  private readonly balancesService = inject(BalancesService);
  private readonly expensesService = inject(ExpensesService);
  private readonly destroyRef = inject(DestroyRef);

  /** The trip whose balances are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message (F5.R12). */
  readonly tripGone = output<void>();

  protected readonly list = signal<BalanceList | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly inconsistent = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;
  protected readonly inconsistentMessage = INCONSISTENT_MESSAGE;
  protected readonly tagText = balanceTagText;
  protected readonly barWidth = barWidthPercent;

  /** "1 person" for one, otherwise "{n} people" (the same wording as the delete dialog). */
  protected readonly countLabel = computed(() => {
    const count = this.list()?.people.length ?? 0;
    return count === 1 ? '1 person' : `${count} people`;
  });

  /**
   * "Nobody owes anything yet." (States table). With no expenses every balance is zero, so this
   * shows when all balances are zero. The response has no expense count.
   */
  protected readonly nobodyOwes = computed(() => {
    const people = this.list()?.people ?? [];
    return people.length > 0 && people.every((person) => person.balanceCents === 0);
  });

  async ngOnInit(): Promise<void> {
    // F5.AC7: an expense added or deleted on this trip changes the balances, so reload them.
    this.expensesService.expensesChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => void this.load());
    await this.load();
  }

  private async load(): Promise<void> {
    // A reload starts clean, so a success after an earlier error shows the list again (F5.AC7).
    this.failed.set(false);
    this.inconsistent.set(false);
    try {
      this.list.set(await this.balancesService.get(this.tripId()));
    } catch (error: unknown) {
      // A 401 is sent to log-in by the API interceptor.
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.tripGone.emit();
      } else if (problemType(error) === INCONSISTENT_TYPE) {
        this.inconsistent.set(true);
      } else {
        this.failed.set(true);
      }
    } finally {
      this.loading.set(false);
    }
  }

  /** The first letter of the name, for the decorative circle (F5.R5). */
  protected initial(person: PersonBalance): string {
    return person.name.charAt(0).toUpperCase();
  }
}
