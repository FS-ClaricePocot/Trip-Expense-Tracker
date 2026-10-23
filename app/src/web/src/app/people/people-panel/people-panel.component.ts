import { Dialog } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import {
  GENERIC_ERROR_MESSAGE,
  PERSON_NAME_REQUIRED_MESSAGE,
  PERSON_NAME_TOO_LONG_MESSAGE,
  duplicatePersonMessage,
  personNameErrorMessage,
  problemType,
} from '../../core/errors/problem-mapper';
import { Person, PeopleService } from '../people.service';
import { RemovePersonDialogComponent, RemovePersonDialogData, RemovePersonResult } from '../remove-person-dialog/remove-person-dialog.component';

/** F3.R2, mirrors the server: the trimmed name is 1 to 100 code points. Any characters are allowed. */
const NAME_MAX_LENGTH = 100;

/** F3.R2: blank after trimming, or more than 100 code points. */
function nameValidator(control: AbstractControl): ValidationErrors | null {
  const name = (control.value ?? '').trim();
  if (name === '') {
    return { name_required: true };
  }
  if ([...name].length > NAME_MAX_LENGTH) {
    return { name_too_long: true };
  }
  return null;
}

/**
 * The People panel on the trip page (F3.R12). Lists the people and holds the
 * "Add a person" field (F3.T7) and the remove control with its confirmation (F3.T8).
 * It has no rename control (F3.R8).
 */
@Component({
  selector: 'app-people-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './people-panel.component.html',
  styleUrl: './people-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PeoplePanelComponent implements OnInit {
  private readonly peopleService = inject(PeopleService);
  private readonly dialog = inject(Dialog);

  /** The trip whose people are listed. */
  readonly tripId = input.required<string>();
  /** Emitted on a 404 (F3.R10): the trip is gone, so the trip page shows its own not-found message. */
  readonly tripGone = output<void>();

  protected readonly people = signal<Person[] | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly genericMessage = GENERIC_ERROR_MESSAGE;

  protected readonly addForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [nameValidator] }),
  });
  /** Shown only after the first submit attempt. */
  protected readonly submitted = signal(false);
  protected readonly adding = signal(false);
  /** A failure with no field to point at, shown above the form. */
  protected readonly formError = signal<string | null>(null);
  /** "{name} was removed from the trip." after a removal (F3.R6). Read out by a status region. */
  protected readonly statusMessage = signal<string | null>(null);

  private readonly nameInput = viewChild.required<ElementRef<HTMLInputElement>>('nameInput');
  private readonly heading = viewChild.required<ElementRef<HTMLHeadingElement>>('heading');

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

  /** Copy for the name field, shown only after the first submit attempt. */
  protected fieldMessage(): string | null {
    const errors = this.addForm.controls.name.errors;
    if (!this.submitted() || !errors) {
      return null;
    }
    // Server field errors are stored as a `server` key with the copy already applied.
    if (typeof errors['server'] === 'string') {
      return errors['server'];
    }
    return errors['name_too_long'] ? PERSON_NAME_TOO_LONG_MESSAGE : PERSON_NAME_REQUIRED_MESSAGE;
  }

  /** Ids of the messages the add field points to, for aria-describedby. */
  protected describedBy(): string | null {
    const ids: string[] = [];
    if (this.fieldMessage()) {
      ids.push('person-name-error');
    }
    if (this.formError() !== null) {
      ids.push('people-message');
    }
    return ids.length > 0 ? ids.join(' ') : null;
  }

  protected submit(): void {
    if (this.adding()) {
      return;
    }
    this.submitted.set(true);
    this.formError.set(null);
    // Re-run the check so a server field error from an earlier attempt is cleared.
    this.addForm.controls.name.updateValueAndValidity();
    if (this.addForm.invalid) {
      this.nameInput().nativeElement.focus();
      return;
    }
    void this.add(this.addForm.getRawValue().name.trim());
  }

  /** F3.R6: opens the confirmation for one person. The dialog returns focus to the control on close. */
  protected openRemove(person: Person): void {
    this.statusMessage.set(null);
    this.formError.set(null);
    const data: RemovePersonDialogData = { tripId: this.tripId(), personId: person.id, name: person.name };
    const ref = this.dialog.open<RemovePersonResult | undefined>(RemovePersonDialogComponent, {
      data,
      role: 'dialog',
      ariaModal: true,
      ariaLabelledBy: 'remove-person-title',
      ariaDescribedBy: 'remove-person-body',
      // Initial focus goes to Cancel, since the action is destructive (F3.R6).
      autoFocus: '.remove-person__cancel',
    });
    ref.closed.subscribe((result) => void this.onRemoveClosed(person, result));
  }

  private async onRemoveClosed(person: Person, result: RemovePersonResult | undefined): Promise<void> {
    if (result === 'removed') {
      this.people.update((list) => (list ?? []).filter((item) => item.id !== person.id));
      this.statusMessage.set(`${person.name} was removed from the trip.`);
      // The control that had focus is gone, so focus goes to the panel heading.
      setTimeout(() => this.heading().nativeElement.focus());
    } else if (result === 'in-use') {
      // F3.R7: the server refused. Reload so the row shows its "In expenses" or "In payments" tag.
      this.formError.set(`${person.name} is in an expense or payment, so they can't be removed.`);
      await this.reload();
    } else if (result === 'missing') {
      // The person was already removed (for example in another tab), or the trip is gone.
      await this.reload();
    }
  }

  /** Reloads the list. A 404 means the trip is gone, so the trip page shows its message (F3.R10). */
  private async reload(): Promise<void> {
    try {
      this.people.set(await this.peopleService.list(this.tripId()));
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.tripGone.emit();
      } else {
        this.formError.set(GENERIC_ERROR_MESSAGE);
      }
    }
  }

  private async add(name: string): Promise<void> {
    this.adding.set(true);
    try {
      const person = await this.peopleService.add(this.tripId(), name);
      this.people.update((list) => [...(list ?? []), person]);
      this.addForm.reset({ name: '' });
      this.submitted.set(false);
    } catch (error: unknown) {
      this.showFailure(error, name);
    } finally {
      this.adding.set(false);
    }
  }

  private showFailure(error: unknown, name: string): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) {
        // F3.R10: the trip is gone, so the trip page shows its not-found message.
        this.tripGone.emit();
        return;
      }
      if (error.status === 400) {
        const message = personNameErrorMessage(error);
        if (message) {
          this.addForm.controls.name.setErrors({ server: message });
          return;
        }
      }
      if (error.status === 409 && problemType(error) === 'duplicate-person-name') {
        this.addForm.controls.name.setErrors({ server: duplicatePersonMessage(name) });
        return;
      }
    }
    this.formError.set(GENERIC_ERROR_MESSAGE);
  }
}
