import { Dialog } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BalancesPanelComponent } from '../../balances/balances-panel/balances-panel.component';
import { SummaryTilesComponent } from '../../balances/summary-tiles/summary-tiles.component';
import { GENERIC_ERROR_MESSAGE, TRIP_NOT_FOUND_MESSAGE } from '../../core/errors/problem-mapper';
import { HeaderComponent } from '../../core/header/header.component';
import { ExpensesPanelComponent } from '../../expenses/expenses-panel/expenses-panel.component';
import { PeoplePanelComponent } from '../../people/people-panel/people-panel.component';
import { SettleUpViewComponent } from '../../settle-up/settle-up-view/settle-up-view.component';
import { TripView, TripViewSwitchComponent } from '../../settle-up/trip-view-switch/trip-view-switch.component';
import { DeleteTripDialogComponent, DeleteTripDialogData } from '../delete-trip-dialog/delete-trip-dialog.component';
import { TRIP_DELETED_STATE, TripDetail, TripsService } from '../trips.service';

/**
 * The trip page (F2.R6 to F2.R9). In F2 it shows the name and a Delete button only;
 * F3 to F6 add the content. It has no rename or edit control (F2.R8).
 * The route is guarded, so only a logged-in user gets here.
 */
@Component({
  selector: 'app-trip-page',
  imports: [
    HeaderComponent,
    RouterLink,
    SummaryTilesComponent,
    PeoplePanelComponent,
    ExpensesPanelComponent,
    BalancesPanelComponent,
    TripViewSwitchComponent,
    SettleUpViewComponent,
  ],
  templateUrl: './trip-page.component.html',
  styleUrl: './trip-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripPageComponent implements OnInit {
  private readonly tripsService = inject(TripsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);

  protected readonly trip = signal<TripDetail | null>(null);
  /** F6.R1: the Expenses view is the default (F6 spec, User flow step 1). */
  protected readonly view = signal<TripView>('expenses');
  /** F2.R9: not owned, missing, or deleted in another tab. */
  protected readonly notFound = signal(false);
  protected readonly failed = signal(false);
  protected readonly notFoundMessage = TRIP_NOT_FOUND_MESSAGE;
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('tripId') ?? '';
    try {
      this.trip.set(await this.tripsService.get(id));
    } catch (error: unknown) {
      // A 401 is sent to log-in by the API interceptor.
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.notFound.set(true);
      } else {
        this.failed.set(true);
      }
    }
  }

  /** F3.R10: the people request got a 404, so the trip is gone. Shows the F2 not-found message once. */
  protected onTripGone(): void {
    this.trip.set(null);
    this.notFound.set(true);
  }

  /** F3.R14: the People panel changed the list, so the delete dialog's people count is updated too. */
  protected onPeopleCountChanged(count: number): void {
    this.trip.update((current) => (current ? { ...current, peopleCount: count } : current));
  }

  /** F2.R7: opens the confirmation. Focus returns to the Delete button when it closes. */
  protected openDelete(): void {
    const trip = this.trip();
    if (!trip) {
      return;
    }
    const data: DeleteTripDialogData = {
      id: trip.id,
      name: trip.name,
      peopleCount: trip.peopleCount,
      expenseCount: trip.expenseCount,
      paymentCount: trip.paymentCount,
    };
    const ref = this.dialog.open<boolean>(DeleteTripDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'delete-trip-title',
      ariaDescribedBy: 'delete-trip-body',
      // Initial focus goes to Cancel, since the action is destructive (F2.R7).
      autoFocus: '.delete-trip__cancel',
    });
    ref.closed.subscribe((deleted) => {
      if (deleted) {
        void this.router.navigate(['/trips'], { state: { [TRIP_DELETED_STATE]: true } });
      }
    });
  }
}
