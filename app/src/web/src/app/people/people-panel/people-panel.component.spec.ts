import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { fireEvent, screen, within } from '@testing-library/angular';
import { ExpensesService, NewExpense } from '../../expenses/expenses.service';
import { Person } from '../people.service';
import { PeoplePanelComponent } from './people-panel.component';

const LOADING = 'Loading people…';
const HELPER = "Names are unique on a trip. People in an expense or payment can't be removed.";
const EMPTY = 'No one is on this trip yet. Add the people who will share its expenses.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

function person(id: string, name: string, inExpense = false, inPayment = false): Person {
  return { id, name, addedAt: '2026-10-01T10:00:00Z', inExpense, inPayment };
}

let fixture: ComponentFixture<PeoplePanelComponent>;
let http: HttpTestingController;

/** Creates the panel for trip-1 and starts its people request. */
function render(): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(PeoplePanelComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets the pending response resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

/** Renders the panel and answers its list request with the given people. */
async function renderWith(people: Person[]): Promise<void> {
  render();
  http.expectOne('/api/trips/trip-1/people').flush(people);
  await settle();
}

function nameInput(): HTMLInputElement {
  return screen.getByLabelText('Add a person') as HTMLInputElement;
}

function typeName(value: string): void {
  const input = nameInput();
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

function pressAdd(): void {
  fireEvent.click(screen.getByRole('button', { name: 'Add' }));
  fixture.detectChanges();
}

describe('PeoplePanelComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the loading text first, then requests the trip people (F3.R12)', async () => {
    render();

    expect(screen.getByText(LOADING)).toBeTruthy();
    http.expectOne('/api/trips/trip-1/people').flush([]);
    await settle();

    expect(screen.queryByText(LOADING)).toBeNull();
  });

  it('renders three people as a list in the order returned (F3.R12, F3.AC10)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam'), person('p2', 'Riley'), person('p3', 'Alex')]);
    await settle();

    const list = screen.getByRole('list');
    const items = within(list).getAllByRole('listitem');
    expect(items.length).toBe(3);
    expect(items.map((item) => item.querySelector('.people-panel__name')?.textContent?.trim())).toEqual(['Sam', 'Riley', 'Alex']);
  });

  it('shows "In expenses" and no remove control for a person in an expense (F3.R7, F3.AC8)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam', true, false)]);
    await settle();

    const item = screen.getByRole('listitem');
    expect(within(item).getByText('In expenses')).toBeTruthy();
    expect(within(item).queryByRole('button')).toBeNull();
  });

  it('reloads the list when an expense is added on this trip, so the tag replaces the remove control (F3.R7)', async () => {
    await renderWith([person('p1', 'Sam')]);
    expect(screen.getByRole('button', { name: 'Remove Sam' })).toBeTruthy();

    void TestBed.inject(ExpensesService).add('trip-1', {} as NewExpense);
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/expenses' }).flush({}, { status: 201, statusText: 'Created' });
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam', true, false)]);
    await settle();

    expect(screen.getByText('In expenses')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Remove Sam' })).toBeNull();
  });

  it('shows "In payments" for a person only in a payment (F3.AC9)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Riley', false, true)]);
    await settle();

    expect(screen.getByText('In payments')).toBeTruthy();
    expect(screen.queryByText('In expenses')).toBeNull();
  });

  it('shows "In expenses" only when a person is in both an expense and a payment (F3.R7)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam', true, true)]);
    await settle();

    expect(screen.getByText('In expenses')).toBeTruthy();
    expect(screen.queryByText('In payments')).toBeNull();
  });

  it('shows the helper text and the people count (F3.R12, F3.R13)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam'), person('p2', 'Riley'), person('p3', 'Alex')]);
    await settle();

    expect(screen.getByRole('heading', { name: 'People' })).toBeTruthy();
    expect(screen.getByText('3 people')).toBeTruthy();
    expect(screen.getByText(HELPER)).toBeTruthy();
  });

  it('shows "1 person" for one person', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam')]);
    await settle();

    expect(screen.getByText('1 person')).toBeTruthy();
  });

  it('shows the empty state when the trip has no people (F3.R12, F3.AC12)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([]);
    await settle();

    expect(screen.getByText(EMPTY)).toBeTruthy();
    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.getByText(HELPER)).toBeTruthy();
  });

  it('emits tripGone and shows no message for a 404 (F3.R10, F3.AC13)', async () => {
    render();
    const emitted: void[] = [];
    fixture.componentInstance.tripGone.subscribe(() => emitted.push(undefined));

    http.expectOne('/api/trips/trip-1/people').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(emitted.length).toBe(1);
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows the generic error for a server error (F3 States)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
  });

  it('has no control to rename a person; the only text field is the add field (F3.R8, F3.AC11)', async () => {
    await renderWith([person('p1', 'Sam')]);

    const textboxes = screen.getAllByRole('textbox');
    expect(textboxes.length).toBe(1);
    expect(textboxes[0].id).toBe('person-name');
    expect(screen.queryByRole('button', { name: /edit|rename/i })).toBeNull();
  });

  it('shows the add field with its label, example placeholder and Add button (F3.R1, F3.AC12)', async () => {
    await renderWith([]);

    expect(nameInput().placeholder).toBe('Morgan');
    expect(screen.getByText(EMPTY)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Add' })).toBeTruthy();
  });

  it('shows the empty and length errors on the field and sends no request (F3.R2, F3.AC3, F3.AC5)', async () => {
    await renderWith([]);

    typeName('   ');
    pressAdd();
    expect(screen.getByText('Enter a name.')).toBeTruthy();
    expect(nameInput().getAttribute('aria-invalid')).toBe('true');
    expect(nameInput().getAttribute('aria-describedby')).toContain('person-name-error');

    typeName('a'.repeat(101));
    pressAdd();
    expect(screen.getByText('A name can be at most 100 characters.')).toBeTruthy();
    http.expectNone({ method: 'POST', url: '/api/trips/trip-1/people' });
  });

  it('sends 100 characters and adds them (F3.AC3)', async () => {
    await renderWith([]);

    typeName('a'.repeat(100));
    pressAdd();
    const req = http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' });
    expect(req.request.body).toEqual({ name: 'a'.repeat(100) });
    req.flush(person('p1', 'a'.repeat(100)), { status: 201, statusText: 'Created' });
    await settle();

    expect(screen.getByText('1 person')).toBeTruthy();
  });

  it('trims the name before sending (F3.AC4)', async () => {
    await renderWith([]);

    typeName('  Jo  ');
    pressAdd();
    const req = http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' });
    expect(req.request.body).toEqual({ name: 'Jo' });
    req.flush(person('p1', 'Jo'), { status: 201, statusText: 'Created' });
    await settle();
  });

  it('shows the duplicate message with the typed trimmed name and adds nothing (F3.R3, F3.AC2)', async () => {
    await renderWith([person('p1', 'Sam')]);

    typeName(' sam ');
    pressAdd();
    const req = http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' });
    req.flush({ type: 'duplicate-person-name', status: 409 }, { status: 409, statusText: 'Conflict' });
    await settle();

    expect(screen.getByText('sam is already on this trip.')).toBeTruthy();
    expect(nameInput().getAttribute('aria-describedby')).toContain('person-name-error');
    expect(screen.getAllByRole('listitem').length).toBe(1);
  });

  it('tells the trip page the new count after an add (F3.R14)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const counts: number[] = [];
    fixture.componentInstance.countChanged.subscribe((count) => counts.push(count));

    typeName('Alex');
    pressAdd();
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(person('p2', 'Alex'), { status: 201, statusText: 'Created' });
    await settle();

    expect(counts).toEqual([2]);
  });

  it('disables the button and shows "Adding…" while the request is sent (F3.R1)', async () => {
    await renderWith([]);

    typeName('Alex');
    pressAdd();
    const busy = screen.getByRole('button', { name: 'Adding…' }) as HTMLButtonElement;
    expect(busy.disabled).toBe(true);

    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(person('p1', 'Alex'), { status: 201, statusText: 'Created' });
    await settle();

    expect(screen.getByRole('button', { name: 'Add' })).toBeTruthy();
  });

  it('adds the person at the end of the list and clears the field (F3.AC1)', async () => {
    await renderWith([person('p1', 'Sam')]);

    typeName('Alex');
    pressAdd();
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(person('p2', 'Alex'), { status: 201, statusText: 'Created' });
    await settle();

    const names = screen.getAllByRole('listitem').map((item) => item.querySelector('.people-panel__name')?.textContent?.trim());
    expect(names).toEqual(['Sam', 'Alex']);
    expect(nameInput().value).toBe('');
    expect(screen.getByText('2 people')).toBeTruthy();
    expect(screen.queryByText('Enter a name.')).toBeNull();
  });

  it('emits tripGone on a 404 when adding (F3.R10)', async () => {
    await renderWith([]);
    const emitted: void[] = [];
    fixture.componentInstance.tripGone.subscribe(() => emitted.push(undefined));

    typeName('Alex');
    pressAdd();
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(emitted.length).toBe(1);
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows the generic error above the form for a server error (F3 States)', async () => {
    await renderWith([]);

    typeName('Alex');
    pressAdd();
    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
  });

  it('submits from the keyboard alone (accessibility)', async () => {
    await renderWith([]);

    expect(screen.getByRole('button', { name: 'Add' }).getAttribute('type')).toBe('submit');
    typeName('Alex');
    fireEvent.submit(nameInput().closest('form') as HTMLFormElement);
    fixture.detectChanges();

    http.expectOne({ method: 'POST', url: '/api/trips/trip-1/people' }).flush(person('p1', 'Alex'), { status: 201, statusText: 'Created' });
    await settle();
  });
});
