import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
import { BalanceList, PersonBalance } from '../balances.service';
import { BalancesPanelComponent } from './balances-panel.component';

const LOADING = 'Loading balances…';
const EMPTY = 'Add people to the trip to see balances.';
const NOBODY_OWES = 'Nobody owes anything yet.';
const FOOTER = 'Balances add up to 0.00';
const INCONSISTENT = 'Balances could not be checked. Please try again.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

function person(id: string, name: string, balanceCents: number): PersonBalance {
  const status = balanceCents > 0 ? 'owed' : balanceCents < 0 ? 'owes' : 'settled';
  return { id, name, balanceCents, status };
}

let fixture: ComponentFixture<BalancesPanelComponent>;
let http: HttpTestingController;

/** Creates the panel for trip-1 and starts its balances request. */
function render(): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(BalancesPanelComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets the pending response resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

/** Renders the panel and answers its balances request with the given body. */
async function renderWith(body: BalanceList): Promise<void> {
  render();
  http.expectOne('/api/trips/trip-1/balances').flush(body);
  await settle();
}

/** Example 1 (product.md): Sam is owed 50.00, Alex owes 10.00, Jo owes 40.00. */
const EXAMPLE_ONE: BalanceList = {
  totalCents: 0,
  people: [person('p1', 'Sam', 5000), person('p2', 'Alex', -1000), person('p3', 'Jo', -4000)],
};

describe('BalancesPanelComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the loading text first, then requests the trip balances (F5.R4)', async () => {
    render();

    expect(screen.getByText(LOADING)).toBeTruthy();
    http.expectOne('/api/trips/trip-1/balances').flush({ totalCents: 0, people: [person('p1', 'Sam', 0)] });
    await settle();

    expect(screen.queryByText(LOADING)).toBeNull();
    expect(screen.getByRole('heading', { name: 'Balances' })).toBeTruthy();
  });

  it('shows Example 1 tags and the footer (F5.AC1, F5.AC2)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(screen.getByText('is owed 50.00')).toBeTruthy();
    expect(screen.getByText('owes 10.00')).toBeTruthy();
    expect(screen.getByText('owes 40.00')).toBeTruthy();
    expect(screen.getByText(FOOTER)).toBeTruthy();
  });

  it('shows a zero balance as "settled up" with no amount (F5.AC3)', async () => {
    await renderWith({ totalCents: 0, people: [person('p1', 'Sam', 0), person('p2', 'Alex', 0)] });

    const items = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(items[0].textContent).toContain('settled up');
    expect(items[0].textContent).not.toMatch(/\d\.\d\d/);
  });

  it('gives the owed tag the green style and the owes tag the red style, with words (F5.AC4)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(screen.getByText('is owed 50.00').classList).toContain('balances-panel__tag--owed');
    expect(screen.getByText('owes 10.00').classList).toContain('balances-panel__tag--owes');
  });

  it('renders rows in the order returned (F5.AC6)', async () => {
    await renderWith(EXAMPLE_ONE);

    const names = within(screen.getByRole('list'))
      .getAllByRole('listitem')
      .map((item) => item.querySelector('.balances-panel__name')?.textContent?.trim());
    expect(names).toEqual(['Sam', 'Alex', 'Jo']);
  });

  it('shows 1842.50 with a thousands separator (F5.AC11)', async () => {
    await renderWith({ totalCents: 0, people: [person('p1', 'Sam', 184250), person('p2', 'Alex', -184250)] });

    expect(screen.getByText('is owed 1,842.50')).toBeTruthy();
    expect(screen.getByText('owes 1,842.50')).toBeTruthy();
  });

  it('clips an 80.00 bar at the end of the track while the tag still reads the full amount (F5.AC17)', async () => {
    await renderWith({ totalCents: 0, people: [person('p1', 'Sam', 8000), person('p2', 'Alex', -8000)] });

    const fills = Array.from(document.querySelectorAll<HTMLElement>('.balances-panel__fill'));
    expect(fills[0].style.width).toBe('50%');
    expect(screen.getByText('is owed 80.00')).toBeTruthy();
  });

  it('hides the bars from assistive tech and keeps the name and tag in the row text (Accessibility)', async () => {
    await renderWith(EXAMPLE_ONE);

    const track = document.querySelector('.balances-panel__track');
    expect(track?.getAttribute('aria-hidden')).toBe('true');
    const row = screen.getByRole('list').querySelector('li');
    expect(row?.textContent).toContain('Sam');
    expect(row?.textContent).toContain('is owed 50.00');
  });

  it('hides the rows and shows the error copy when the balances are inconsistent (F5.AC16)', async () => {
    render();
    http
      .expectOne('/api/trips/trip-1/balances')
      .flush({ type: 'balances_inconsistent', title: 'Balances could not be checked.' }, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(INCONSISTENT);
    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.queryByText(FOOTER)).toBeNull();
  });

  it('shows the generic error for another server failure', async () => {
    render();
    http.expectOne('/api/trips/trip-1/balances').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
  });

  it('emits tripGone on a 404 (F5.R12)', async () => {
    render();
    let gone = 0;
    fixture.componentInstance.tripGone.subscribe(() => gone++);
    http.expectOne('/api/trips/trip-1/balances').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(gone).toBe(1);
  });

  it('shows the empty-state text when the trip has no people', async () => {
    await renderWith({ totalCents: 0, people: [] });

    expect(screen.getByText(EMPTY)).toBeTruthy();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('shows "Nobody owes anything yet." when every balance is zero (States table)', async () => {
    await renderWith({ totalCents: 0, people: [person('p1', 'Sam', 0), person('p2', 'Alex', 0)] });

    expect(screen.getByText(NOBODY_OWES)).toBeTruthy();
  });

  it('has no input control (F5.AC15)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(document.querySelector('input, select, textarea')).toBeNull();
  });

  it('labels the list with the number of people (count announced)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(screen.getByRole('list', { name: '3 people' })).toBeTruthy();
  });
});
