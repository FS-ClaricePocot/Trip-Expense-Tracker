import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import {
  TRIP_LIMIT_MESSAGE,
  fieldErrorMessage,
  fieldErrorMessages,
  formErrorMessage,
  problemType,
} from '../../core/errors/problem-mapper';
import { TripSummary, TripsService } from '../trips.service';

/** F2.R2: the trimmed name is 1 to 100 code points. Any characters are allowed. */
const NAME_MAX_LENGTH = 100;

/** F2.R2, mirrors the server: blank after trimming, or more than 100 code points. */
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
 * The create form on the trip list page (F2.R1 to F2.R4, F2.R11). Emits the new
 * trip on success, and emits again when the server says the 50-trip limit is reached
 * so the list can reload.
 */
@Component({
  selector: 'app-trip-create-form',
  imports: [ReactiveFormsModule],
  templateUrl: './trip-create-form.component.html',
  styleUrl: './trip-create-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripCreateFormComponent {
  private readonly tripsService = inject(TripsService);

  /** True when the list already holds 50 trips (F2.R4). */
  readonly atLimit = input(false);
  readonly created = output<TripSummary>();
  readonly tripLimitReached = output<void>();

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [nameValidator] }),
  });

  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);
  /** Set when the server reports a same-name trip (F2.R3). Holds the trimmed name. */
  protected readonly duplicateName = signal<string | null>(null);
  /** Set when the server reports the 50-trip limit (F2.R4). */
  private readonly limitHit = signal(false);
  protected readonly limited = computed(() => this.atLimit() || this.limitHit());
  protected readonly limitMessage = TRIP_LIMIT_MESSAGE;

  private readonly nameInput = viewChild.required<ElementRef<HTMLInputElement>>('nameInput');

  constructor() {
    // A disabled form shows the limit message and sends nothing (F2.R4).
    effect(() => {
      if (this.limited()) {
        this.form.disable({ emitEvent: false });
      } else {
        this.form.enable({ emitEvent: false });
      }
    });
  }

  /** Copy for the name field, shown only after the first submit attempt. */
  protected fieldMessage(): string | null {
    const errors = this.form.controls.name.errors;
    if (!this.submitted() || !errors) {
      return null;
    }
    // Server field errors are stored as a `server` key with the copy already applied.
    if (typeof errors['server'] === 'string') {
      return errors['server'];
    }
    return fieldErrorMessage(Object.keys(errors)[0]);
  }

  /** Ids of the messages the name field points to, for aria-describedby. */
  protected describedBy(): string | null {
    const ids: string[] = [];
    if (this.fieldMessage()) {
      ids.push('trip-name-error');
    }
    if (this.limited() || this.duplicateName() !== null || this.formError() !== null) {
      ids.push('trip-message');
    }
    return ids.length > 0 ? ids.join(' ') : null;
  }

  protected submit(): void {
    console.log('Added Trip');
    if (this.submitting() || this.limited()) {
      return;
    }
    this.submitted.set(true);
    this.formError.set(null);
    // Re-run the check so a server field error from an earlier attempt is cleared.
    this.form.controls.name.updateValueAndValidity();
    if (this.form.invalid) {
      this.nameInput().nativeElement.focus();
      return;
    }
    void this.send(this.form.getRawValue().name.trim(), false);
  }

  /** F2.R3: the user chose Create anyway, so the same name is sent with the confirmation. */
  protected createAnyway(): void {
    const name = this.duplicateName();
    if (name !== null) {
      void this.send(name, true);
    }
  }

  /** F2.R3: the user chose Cancel. Nothing is created and the typed text stays. */
  protected cancelDuplicate(): void {
    this.duplicateName.set(null);
    this.nameInput().nativeElement.focus();
  }

  private async send(name: string, confirmDuplicate: boolean): Promise<void> {
    this.submitting.set(true);
    this.formError.set(null);
    this.duplicateName.set(null);
    try {
      const trip = await this.tripsService.create(confirmDuplicate ? { name, confirmDuplicate: true } : { name });
      this.form.reset({ name: '' });
      this.submitted.set(false);
      this.created.emit(trip);
    } catch (error: unknown) {
      this.showFailure(error, name);
    } finally {
      this.submitting.set(false);
    }
  }

  private showFailure(error: unknown, name: string): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) {
        const message = fieldErrorMessages(error)['name'];
        if (message) {
          this.form.controls.name.setErrors({ server: message });
          return;
        }
      }
      if (error.status === 409) {
        const type = problemType(error);
        if (type === 'duplicate-trip-name') {
          this.duplicateName.set(name);
          return;
        }
        if (type === 'trip-limit-reached') {
          this.limitHit.set(true);
          this.tripLimitReached.emit();
          return;
        }
      }
    }
    this.formError.set(formErrorMessage(error));
  }
}
