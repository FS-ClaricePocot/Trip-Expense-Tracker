import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
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

  it('has no control to rename a person (F3.R8, F3.AC11)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam')]);
    await settle();

    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('button', { name: /edit|rename/i })).toBeNull();
  });
});
