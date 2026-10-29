import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/angular';
import { PeopleService, Person } from '../../people/people.service';
import { Expense } from '../expenses.service';
import { AddExpenseFormComponent } from './add-expense-form.component';

const DESCRIPTION_REQUIRED = 'Enter a description.';
const DESCRIPTION_TOO_LONG = 'A description can be at most 100 characters.';
const AMOUNT_REQUIRED = 'Enter an amount.';
const AMOUNT_INVALID = 'Enter an amount like 12.50.';
const AMOUNT_NOT_POSITIVE = 'Enter an amount greater than 0.00.';
const AMOUNT_DECIMALS = 'An amount can have at most 2 decimal places.';
const AMOUNT_TOO_LARGE = 'That amount is too large to record.';
const PAYER_REQUIRED = 'Choose who paid.';
const PARTICIPANTS_REQUIRED = 'Choose at least one person to split between.';
const NO_PEOPLE = 'Add people to the trip before adding an expense.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const ADDING = 'Adding…';
const SPLIT_HINT = 'Split equally · leftover cents go to people in the order they were added';
const EXPENSES_URL = '/api/trips/trip-1/expenses';

function person(id: string, name: string): Person {
  return { id, name, addedAt: '2026-10-01T10:00:00Z', inExpense: false, inPayment: false };
}

const SAM = person('p-sam', 'Sam');
const ALEX = person('p-alex', 'Alex');
const LEE = person('p-lee', 'Lee');
const PEOPLE = [SAM, ALEX, LEE];

const SAVED: Expense = {
  id: 'e1',
  description: 'Dinner',
  amountCents: 1050,
  payer: { id: 'p-sam', name: 'Sam' },
  participantCount: 3,
  participants: [],
  createdAt: '2026-10-26T10:00:00Z',
};

/** Lets the promise chain after a flushed response finish before the DOM is checked. */
function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 50));
}

/** Renders the form for trip-1 and answers its people request with the given list. */
async function renderForm(people: Person[] = PEOPLE) {
  const { fixture } = await render(AddExpenseFormComponent, {
    componentInputs: { tripId: 'trip-1' },
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  const http = TestBed.inject(HttpTestingController);
  http.expectOne('/api/trips/trip-1/people').flush(people);
  await settle();
  return { fixture, http };
}

function typeDescription(value: string): void {
  fireEvent.input(screen.getByLabelText('What was it for?'), { target: { value } });
}

function typeAmount(value: string): void {
  fireEvent.input(screen.getByLabelText('Amount'), { target: { value } });
}

function choosePayer(id: string): void {
  fireEvent.change(screen.getByLabelText('Paid by'), { target: { value: id } });
}

function toggle(name: string): void {
  fireEvent.click(screen.getByRole('checkbox', { name }));
}

function submit(): void {
  fireEvent.submit(screen.getByRole('button', { name: /^Add expense$|^Adding…$/ }).closest('form')!);
}

function fillValid(): void {
  typeDescription('Dinner');
  typeAmount('10.50');
  choosePayer('p-sam');
}

describe('AddExpenseFormComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('opens with every person checked and no payer selected (F4.R4, F4.R5, F4.AC9)', async () => {
    await renderForm();

    expect(screen.getByRole('checkbox', { name: 'Sam' }).matches(':checked')).toBeTrue();
    expect(screen.getByRole('checkbox', { name: 'Alex' }).matches(':checked')).toBeTrue();
    expect(screen.getByRole('checkbox', { name: 'Lee' }).matches(':checked')).toBeTrue();
    expect((screen.getByLabelText('Paid by') as HTMLSelectElement).value).toBe('');
  });

  it('shows the visible labels, the legend and the helper text (F4.T8)', async () => {
    await renderForm();

    expect(screen.getByLabelText('What was it for?')).toBeTruthy();
    expect(screen.getByLabelText('Amount').getAttribute('inputmode')).toBe('decimal');
    expect(screen.getByLabelText('Paid by')).toBeTruthy();
    expect(screen.getByRole('group', { name: 'Split between' })).toBeTruthy();
    expect(screen.getByText(SPLIT_HINT)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add expense' })).toBeTruthy();
  });

  it('refuses a blank description with the exact copy, links it, and sends nothing (F4.R2, F4.AC5)', async () => {
    const { http } = await renderForm();
    typeDescription('   ');
    choosePayer('p-sam');
    typeAmount('10');
    submit();

    await waitFor(() => expect(screen.getByText(DESCRIPTION_REQUIRED)).toBeTruthy());
    const input = screen.getByLabelText('What was it for?');
    expect(input.getAttribute('aria-describedby')).toContain('expense-description-error');
    expect(input.getAttribute('aria-invalid')).toBe('true');
    http.expectNone(EXPENSES_URL);
  });

  it('refuses a description of 101 characters and accepts 100 (F4.R2, F4.AC5)', async () => {
    const { http } = await renderForm();
    typeDescription('x'.repeat(101));
    choosePayer('p-sam');
    typeAmount('10');
    submit();

    await waitFor(() => expect(screen.getByText(DESCRIPTION_TOO_LONG)).toBeTruthy());
    http.expectNone(EXPENSES_URL);

    typeDescription('x'.repeat(100));
    submit();
    const request = http.expectOne(EXPENSES_URL);
    expect(request.request.body.description).toBe('x'.repeat(100));
    request.flush(SAVED, { status: 201, statusText: 'Created' });
    await settle();
  });

  it('refuses an empty amount with the exact copy and sends nothing (F4.R3)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('');
    submit();

    await waitFor(() => expect(screen.getByText(AMOUNT_REQUIRED)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('refuses text that is not a decimal with the exact copy (F4.R3)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('12,50');
    submit();

    await waitFor(() => expect(screen.getByText(AMOUNT_INVALID)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('refuses zero with the greater-than copy (F4.AC3)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('0');
    submit();

    await waitFor(() => expect(screen.getByText(AMOUNT_NOT_POSITIVE)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('refuses 10.505 with the decimal-places copy (F4.AC4)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('10.505');
    submit();

    await waitFor(() => expect(screen.getByText(AMOUNT_DECIMALS)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('refuses an amount too large to store with the exact copy (F4.R3)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('92233720368547758.08');
    submit();

    await waitFor(() => expect(screen.getByText(AMOUNT_TOO_LARGE)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('sends 10.5 unchanged as text, with everyone checked, in the order listed (F4.AC4, F4.R5)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('10.5');
    submit();

    const request = http.expectOne(EXPENSES_URL);
    expect(request.request.body).toEqual({
      description: 'Dinner',
      amount: '10.5',
      payerPersonId: 'p-sam',
      participantPersonIds: ['p-sam', 'p-alex', 'p-lee'],
    });
    request.flush(SAVED, { status: 201, statusText: 'Created' });
    await settle();
  });

  it('refuses no payer with the exact copy and sends nothing (F4.R4, F4.AC6)', async () => {
    const { http } = await renderForm();
    typeDescription('Dinner');
    typeAmount('10');
    submit();

    await waitFor(() => expect(screen.getByText(PAYER_REQUIRED)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('shows the participant copy above the checkboxes when every person is unchecked (F4.R5, F4.AC7)', async () => {
    const { http } = await renderForm();
    fillValid();
    toggle('Sam');
    toggle('Alex');
    toggle('Lee');
    submit();

    const group = screen.getByRole('group', { name: 'Split between' });
    await waitFor(() => expect(within(group).getByText(PARTICIPANTS_REQUIRED)).toBeTruthy());
    http.expectNone(EXPENSES_URL);
  });

  it('disables the form and shows the message when the trip has no people (F4.AC10)', async () => {
    await renderForm([]);

    expect(screen.getByText(NO_PEOPLE)).toBeTruthy();
    expect((screen.getByLabelText('What was it for?') as HTMLInputElement).disabled).toBeTrue();
    expect((screen.getByLabelText('Amount') as HTMLInputElement).disabled).toBeTrue();
    expect((screen.getByLabelText('Paid by') as HTMLSelectElement).disabled).toBeTrue();
    expect((screen.getByRole('button', { name: 'Add expense' }) as HTMLButtonElement).disabled).toBeTrue();
  });

  it('enables the form when a person is added to the trip while it is open, without a reload (F4.AC10)', async () => {
    const { http } = await renderForm([]);
    const added = TestBed.inject(PeopleService).add('trip-1', 'Sam');
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(SAM);
    await added;
    http.expectOne('/api/trips/trip-1/people').flush([SAM]);
    await settle();

    expect(screen.queryByText(NO_PEOPLE)).toBeNull();
    expect(screen.getByRole('checkbox', { name: 'Sam' })).toBeTruthy();
    expect((screen.getByLabelText('What was it for?') as HTMLInputElement).disabled).toBeFalse();
    expect((screen.getByRole('button', { name: 'Add expense' }) as HTMLButtonElement).disabled).toBeFalse();
  });

  it('shows Adding… and disables the button while the request is in flight, then resets on success (F4.R1, F4.AC9)', async () => {
    const { fixture, http } = await renderForm();
    const added: Expense[] = [];
    fixture.componentInstance.added.subscribe((expense: Expense) => added.push(expense));
    fillValid();
    submit();

    const pending = screen.getByRole('button', { name: ADDING }) as HTMLButtonElement;
    expect(pending.disabled).toBeTrue();

    http.expectOne(EXPENSES_URL).flush(SAVED, { status: 201, statusText: 'Created' });
    await settle();

    expect(added).toEqual([SAVED]);
    expect((screen.getByLabelText('What was it for?') as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText('Amount') as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText('Paid by') as HTMLSelectElement).value).toBe('');
    expect(screen.getByRole('checkbox', { name: 'Lee' }).matches(':checked')).toBeTrue();
    expect(screen.queryByText(GENERIC_ERROR)).toBeNull();
    expect((screen.getByRole('button', { name: 'Add expense' }) as HTMLButtonElement).disabled).toBeFalse();
  });

  it('shows the server copy for a field the server refuses (F4.R3)', async () => {
    const { http } = await renderForm();
    fillValid();
    typeAmount('10');
    submit();

    http
      .expectOne(EXPENSES_URL)
      .flush(
        { errors: { amount: ['amount_too_many_decimals'] }, status: 400, title: 'Bad request' },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();

    expect(screen.getByText(AMOUNT_DECIMALS)).toBeTruthy();
  });

  it('shows the generic message above the fields when the request fails with a server error', async () => {
    const { http } = await renderForm();
    fillValid();
    submit();

    http.expectOne(EXPENSES_URL).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByText(GENERIC_ERROR)).toBeTruthy();
  });

  it('uses native controls, so the keyboard reaches every field and the button (F4.T8)', async () => {
    await renderForm();

    expect((screen.getByLabelText('What was it for?') as HTMLElement).tagName).toBe('INPUT');
    expect((screen.getByLabelText('Paid by') as HTMLElement).tagName).toBe('SELECT');
    expect((screen.getByRole('checkbox', { name: 'Sam' }) as HTMLInputElement).type).toBe('checkbox');
    expect((screen.getByRole('button', { name: 'Add expense' }) as HTMLButtonElement).type).toBe('submit');
  });
});
