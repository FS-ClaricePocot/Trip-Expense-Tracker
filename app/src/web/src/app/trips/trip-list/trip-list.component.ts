import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HeaderComponent } from '../../core/header/header.component';
import { TripCreateFormComponent } from '../trip-create-form/trip-create-form.component';
import { TRIP_DELETED_STATE, TRIP_LIMIT, TripSummary, TripsService } from '../trips.service';
import { tripPeopleText } from './trip-count-text';

/**
 * "Your trips" page (F2.R5, F2.R10, F2.R11). Replaces the F1 placeholder (F1.R16).
 * The route is guarded, so only a logged-in user gets here.
 */
@Component({
  selector: 'app-trip-list',
  imports: [HeaderComponent, RouterLink, TripCreateFormComponent],
  templateUrl: './trip-list.component.html',
  styleUrl: './trip-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripListComponent implements OnInit {
  private readonly tripsService = inject(TripsService);
  private readonly router = inject(Router);

  /** F2.R7: true when the trip page sent the user here after a delete. Read from navigation state, not stored. */
  protected readonly tripDeleted = this.router.getCurrentNavigation()?.extras.state?.[TRIP_DELETED_STATE] === true;
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly trips = signal<TripSummary[]>([]);
  protected readonly atLimit = computed(() => this.trips().length >= TRIP_LIMIT);
  protected readonly tripPeopleText = tripPeopleText;

  async ngOnInit(): Promise<void> {
    try {
      this.trips.set(await this.tripsService.list());
    } catch {
      // A 401 is sent to log-in by the API interceptor. Any other failure shows the message.
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  /** F2.AC1: the new trip goes to the top of the list. */
  protected onCreated(trip: TripSummary): void {
    this.trips.update((trips) => [trip, ...trips]);
  }

  /** F2.R4: the server refused a create at 50 trips, so the list is loaded again. */
  protected async reload(): Promise<void> {
    try {
      this.trips.set(await this.tripsService.list());
      this.failed.set(false);
    } catch {
      this.failed.set(true);
    }
  }
}
