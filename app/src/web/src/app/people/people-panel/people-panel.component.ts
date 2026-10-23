import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { GENERIC_ERROR_MESSAGE } from '../../core/errors/problem-mapper';
import { Person, PeopleService } from '../people.service';

/**
 * The People panel on the trip page (F3.R12). Read-only in F3.T6; the add field
 * (F3.T7) and the remove control (F3.T8) come later. It has no rename control (F3.R8).
 */
@Component({
  selector: 'app-people-panel',
  templateUrl: './people-panel.component.html',
  styleUrl: './people-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PeoplePanelComponent implements OnInit {
  private readonly peopleService = inject(PeopleService);

  /** The trip whose people are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404 (F3.R10): the trip is gone, so the trip page shows its own not-found message. */
  readonly tripGone = output<void>();

  protected readonly people = signal<Person[] | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;

  /** "1 person" for one, otherwise "{n} people" (the same wording as the trip card, F3.R13). */
  protected readonly countText = computed(() => {
    const count = this.people()?.length ?? 0;
    return count === 1 ? '1 person' : `${count} people`;
  });

  async ngOnInit(): Promise<void> {
    try {
      this.people.set(await this.peopleService.list(this.tripId()));
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

  /** The avatar is decorative (hidden from screen readers in the template). */
  protected initial(name: string): string {
    return name.charAt(0).toUpperCase();
  }
}
