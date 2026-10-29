import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { filter } from 'rxjs';
import {
  EXPENSE_AMOUNT_INVALID_MESSAGE,
  EXPENSE_AMOUNT_NOT_POSITIVE_MESSAGE,
  EXPENSE_AMOUNT_REQUIRED_MESSAGE,
  EXPENSE_AMOUNT_TOO_LARGE_MESSAGE,
  EXPENSE_AMOUNT_TOO_MANY_DECIMALS_MESSAGE,
  EXPENSE_DESCRIPTION_REQUIRED_MESSAGE,
  EXPENSE_DESCRIPTION_TOO_LONG_MESSAGE,
  EXPENSE_NO_PEOPLE_MESSAGE,
  EXPENSE_PARTICIPANTS_REQUIRED_MESSAGE,
  EXPENSE_PAYER_REQUIRED_MESSAGE,
  GENERIC_ERROR_MESSAGE,
  expenseFieldErrorMessages,
} from '../../core/errors/problem-mapper';
import { Person, PeopleService } from '../../people/people.service';
import { Expense, ExpensesService, NewExpense } from '../expenses.service';

/** F4.R2: the trimmed description is 1 to 100 code points. Mirrors the server. */
const DESCRIPTION_MAX_LENGTH = 100;
/** F4.R3: the largest value of the stored whole-cent amount (a bigint column). */
const MAX_CENTS = 9223372036854775807n;

/** The fields in the order they appear, so the first invalid one gets focus. Names match the server's ProblemDetails fields. */
const FIELD_ORDER = ['description', 'amount', 'payerPersonId', 'participantPersonIds'];

/** Where focus goes for each field when a check fails. */
const FOCUS_SELECTORS: Record<string, string> = {
  description: '#expense-description',
  amount: '#expense-amount',
  payerPersonId: '#expense-payer',
  participantPersonIds: '.add-expense__person input',
};

/** The id of each field's error message, for aria-describedby. */
const ERROR_IDS: Record<string, string> = {
  description: 'expense-description-error',
  amount: 'expense-amount-error',
  payerPersonId: 'expense-payer-error',
  participantPersonIds: 'expense-participants-error',
};

/** F4.R2: the copy for a description that is blank after trimming, or too long. */
function descriptionErrorMessage(text: string): string | null {
  const value = text.trim();
  if (value === '') {
    return EXPENSE_DESCRIPTION_REQUIRED_MESSAGE;
  }
  return [...value].length > DESCRIPTION_MAX_LENGTH ? EXPENSE_DESCRIPTION_TOO_LONG_MESSAGE : null;
}

/**
 * F4.R3: the copy for an amount that the server would refuse, or null when it is valid.
 * Only the form's checks live here; the server is the authority and parses the text again.
 */
function amountErrorMessage(text: string): string | null {
  const value = text.trim();
  if (value === '') {
    return EXPENSE_AMOUNT_REQUIRED_MESSAGE;
  }
  const match = /^(\d+)(?:\.(\d+))?$/.exec(value);
  if (match === null) {
    return EXPENSE_AMOUNT_INVALID_MESSAGE;
  }
  const fraction = match[2] ?? '';
  if (fraction.length > 2) {
    return EXPENSE_AMOUNT_TOO_MANY_DECIMALS_MESSAGE;
  }
  const cents = BigInt(match[1]) * 100n + BigInt(fraction.padEnd(2, '0'));
  if (cents > MAX_CENTS) {
    return EXPENSE_AMOUNT_TOO_LARGE_MESSAGE;
  }
  return cents === 0n ? EXPENSE_AMOUNT_NOT_POSITIVE_MESSAGE : null;
}

/**
 * The add-expense form in the Expenses panel (F4.R1 to F4.R6, form side). Loads the trip's
 * people for the payer and the checkboxes, and emits the saved expense on success. It is
 * disabled when the trip has no people (F4.AC10).
 */
@Component({
  selector: 'app-add-expense-form',
  imports: [ReactiveFormsModule],
  templateUrl: './add-expense-form.component.html',
  styleUrl: './add-expense-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddExpenseFormComponent implements OnInit {
  private readonly expensesService = inject(ExpensesService);
  private readonly peopleService = inject(PeopleService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  /** The trip the expense is added to. */
  readonly tripId = input.required<string>();
  /** Emitted with the saved expense (F4.R8: it goes to the top of the list). */
  readonly added = output<Expense>();
  /** Emitted on a 404: the trip is gone, so the trip page shows its own not-found message. */
  readonly tripGone = output<void>();

  protected readonly people = signal<Person[]>([]);
  protected readonly loading = signal(true);
  private readonly loadFailed = signal(false);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  /** A failure with no field to point at, shown above the fields. */
  protected readonly formError = signal<string | null>(null);
  /** Copy from the server for one field, keyed by the server's field name. Cleared on each submit. */
  private readonly serverErrors = signal<Record<string, string>>({});

  /** F4.AC10: no people on the trip. */
  protected readonly noPeople = computed(() => !this.loading() && !this.loadFailed() && this.people().length === 0);
  /** The form can be sent only when the people are loaded and there is at least one. */
  protected readonly ready = computed(() => !this.loading() && this.people().length > 0);

  protected readonly form = new FormGroup({
    description: new FormControl('', { nonNullable: true }),
    amount: new FormControl('', { nonNullable: true }),
    payerPersonId: new FormControl('', { nonNullable: true }),
    /** One checkbox per person, keyed by person id. All start checked (F4.R5). */
    participants: new FormGroup<Record<string, AbstractControl<boolean>>>({}),
  });

  async ngOnInit(): Promise<void> {
    this.form.disable({ emitEvent: false });
    // F4.AC10: someone added or removed in the People panel while this form is open, so reload the list.
    this.peopleService.peopleChanged
      .pipe(
        filter((tripId) => tripId === this.tripId()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => void this.loadPeople());
    await this.loadPeople();
  }

  /** The copy for a field, or null. Server copy shows at once; client copy shows after a submit attempt. */
  protected message(field: string): string | null {
    const server = this.serverErrors()[field];
    if (server !== undefined) {
      return server;
    }
    return this.submitted() ? (this.clientErrors()[field] ?? null) : null;
  }

  /** The copy above the fields: no people, or a failure. */
  protected topMessage(): string | null {
    return this.noPeople() ? EXPENSE_NO_PEOPLE_MESSAGE : this.formError();
  }

  /** Ids of the messages a field points to, for aria-describedby. */
  protected describedBy(field: string): string | null {
    const ids: string[] = [];
    if (this.topMessage()) {
      ids.push('expense-message');
    }
    if (this.message(field)) {
      ids.push(ERROR_IDS[field]);
    }
    return ids.length > 0 ? ids.join(' ') : null;
  }

  protected submit(): void {
    if (this.submitting() || !this.ready()) {
      return;
    }
    this.submitted.set(true);
    this.formError.set(null);
    this.serverErrors.set({});
    const errors = this.clientErrors();
    const first = FIELD_ORDER.find((field) => errors[field] !== undefined);
    if (first !== undefined) {
      this.host.nativeElement.querySelector<HTMLElement>(FOCUS_SELECTORS[first])?.focus();
      return;
    }
    void this.add();
  }

  /** The client checks, which mirror the server codes and their copy (F4.R2 to F4.R5). */
  private clientErrors(): Record<string, string> {
    const errors: Record<string, string> = {};
    const description = descriptionErrorMessage(this.form.controls.description.value);
    const amount = amountErrorMessage(this.form.controls.amount.value);
    if (description) {
      errors['description'] = description;
    }
    if (amount) {
      errors['amount'] = amount;
    }
    if (this.form.controls.payerPersonId.value === '') {
      errors['payerPersonId'] = EXPENSE_PAYER_REQUIRED_MESSAGE;
    }
    if (this.checkedIds().length === 0) {
      errors['participantPersonIds'] = EXPENSE_PARTICIPANTS_REQUIRED_MESSAGE;
    }
    return errors;
  }

  /** The checked people, in the order they were added (F4.R7). */
  private checkedIds(): string[] {
    const checks = this.form.controls.participants.controls;
    return this.people()
      .filter((person) => checks[person.id]?.value === true)
      .map((person) => person.id);
  }

  private async add(): Promise<void> {
    this.submitting.set(true);
    const body: NewExpense = {
      description: this.form.controls.description.value.trim(),
      // F4.R3: the amount text goes to the server unchanged; the server is the authority.
      amount: this.form.controls.amount.value,
      payerPersonId: this.form.controls.payerPersonId.value,
      participantPersonIds: this.checkedIds(),
    };
    try {
      const saved = await this.expensesService.add(this.tripId(), body);
      this.reset();
      this.added.emit(saved);
    } catch (error: unknown) {
      this.showFailure(error);
    } finally {
      this.submitting.set(false);
    }
  }

  /** The form opens again as it did: empty text, no payer, everyone checked, no message (F4.AC9). */
  private reset(): void {
    this.form.reset();
    this.submitted.set(false);
    this.serverErrors.set({});
    this.formError.set(null);
  }

  private showFailure(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) {
        // F4.R15: the trip is gone, so the trip page shows its not-found message.
        this.tripGone.emit();
        return;
      }
      if (error.status === 400) {
        const messages = expenseFieldErrorMessages(error);
        if (Object.keys(messages).length > 0) {
          this.serverErrors.set(messages);
          return;
        }
      }
      if (error.status === 409) {
        // no_people_on_trip or people_changed: the people changed, so reload them.
        this.formError.set(GENERIC_ERROR_MESSAGE);
        void this.loadPeople();
        return;
      }
    }
    this.formError.set(GENERIC_ERROR_MESSAGE);
  }

  /** Loads the people. Each person gets a checkbox, all checked (F4.R5). */
  private async loadPeople(): Promise<void> {
    try {
      this.setPeople(await this.peopleService.list(this.tripId()));
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.tripGone.emit();
      } else {
        this.loadFailed.set(true);
        this.form.disable({ emitEvent: false });
        this.formError.set(GENERIC_ERROR_MESSAGE);
      }
    } finally {
      this.loading.set(false);
    }
  }

  /** Rebuilds the checkboxes for the list, all checked, and disables the form when there are no people (F4.AC10). */
  private setPeople(list: Person[]): void {
    const group = this.form.controls.participants;
    for (const key of Object.keys(group.controls)) {
      group.removeControl(key);
    }
    for (const person of list) {
      group.addControl(person.id, new FormControl(true, { nonNullable: true }));
    }
    this.people.set(list);
    if (list.length === 0) {
      this.form.disable({ emitEvent: false });
    } else {
      this.form.enable({ emitEvent: false });
    }
  }
}
