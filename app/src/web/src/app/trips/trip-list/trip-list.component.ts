import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HeaderComponent } from '../../core/header/header.component';
import { TripSummary, TripsService } from '../trips.service';

/**
 * "Your trips" page (F2.R5, F2.R10, F2.R11). Replaces the F1 placeholder (F1.R16).
 * The route is guarded, so only a logged-in user gets here.
 */
@Component({
  selector: 'app-trip-list',
  imports: [HeaderComponent, RouterLink],
  templateUrl: './trip-list.component.html',
  styleUrl: './trip-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripListComponent implements OnInit {
  private readonly tripsService = inject(TripsService);

  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly trips = signal<TripSummary[]>([]);

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
}
