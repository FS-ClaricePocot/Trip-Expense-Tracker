import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
import { Expense, ExpenseList } from '../expenses.service';
import { ExpensesPanelComponent } from './expenses-panel.component';

const LOADING = 'Loading expenses…';
const EMPTY = 'No expenses yet. Add the first one below.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

function expense(id: string, description: string, amountCents: number, participantCount: number, payerName = 'Alex'): Expense {
  return {
    id,
    description,
    amountCents,
    payer: { id: `p-${payerName}`, name: payerName },
    participantCount,
    participants: [],
    createdAt: '2026-10-01T10:00:00Z',
  };
}

function listOf(expenses: Expense[]): ExpenseList {
  const total = expenses.reduce((sum, item) => sum + item.amountCents, 0);
  return { total, count: expenses.length, expenses };
}

let fixture: ComponentFixture<ExpensesPanelComponent>;
let http: HttpTestingController;

/** Creates the panel for trip-1 and starts its expenses request. */
function render(): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(ExpensesPanelComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets the pending response resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

/** Renders the panel and answers its list request with the given body. */
async function renderWith(body: ExpenseList): Promise<void> {
  render();
  http.expectOne('/api/trips/trip-1/expenses').flush(body);
  await settle();
}

describe('ExpensesPanelComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the loading text first, then requests the trip expenses (F4.R8)', async () => {
    render();

    expect(screen.getByText(LOADING)).toBeTruthy();
    http.expectOne('/api/trips/trip-1/expenses').flush(listOf([]));
    await settle();

    expect(screen.queryByText(LOADING)).toBeNull();
    expect(screen.getByRole('heading', { name: 'Expenses' })).toBeTruthy();
  });

  it('renders three expenses as a list in the order returned, with the split text (F4.R8, F4.R10, F4.AC17)', async () => {
    await renderWith(
      listOf([expense('e3', 'Dinner', 3000, 3), expense('e2', 'Taxi', 1200, 2), expense('e1', 'Train', 900, 3)]),
    );

    const items = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(items.length).toBe(3);
    expect(items.map((item) => item.querySelector('.expenses-panel__description')?.textContent?.trim())).toEqual([
      'Dinner',
      'Taxi',
      'Train',
    ]);
    expect(screen.getAllByText('Alex paid · split 3 ways').length).toBe(2);
    expect(screen.getByText('Alex paid · split 2 ways')).toBeTruthy();
  });

  it('shows the total and the count equal to the sum of the amounts (F4.AC16, F4.R13)', async () => {
    await renderWith(listOf([expense('e1', 'Lunch', 1050, 2), expense('e2', 'Fuel', 2000, 3), expense('e3', 'Tip', 5, 1)]));

    expect(screen.getByText('3 expenses')).toBeTruthy();
    expect(screen.getByText('30.55')).toBeTruthy();
  });

  it('uses the singular for one expense and one person sharing it (F4.R10)', async () => {
    await renderWith(listOf([expense('e1', 'Coffee', 450, 1, 'Sam')]));

    expect(screen.getByText('1 expense')).toBeTruthy();
    expect(screen.getByText('Sam paid · split 1 way')).toBeTruthy();
  });

  it('shows no per-person share and no edit control on a row (F4.R10, F4.R12, F4.AC15, F4.AC17)', async () => {
    await renderWith(listOf([expense('e1', 'Hotel', 9000, 3)]));

    const item = screen.getByRole('listitem');
    expect(within(item).queryByRole('button')).toBeNull();
    expect(within(item).queryByRole('textbox')).toBeNull();
    expect(item.textContent).not.toContain('3.00');
    expect(item.textContent).toContain('Hotel');
    expect(item.textContent).toContain('90.00');
  });

  it('hides the generic money icon from screen readers on every row (F4.R11 interim)', async () => {
    await renderWith(listOf([expense('e1', 'Hotel', 9000, 3), expense('e2', 'Taxi', 1200, 2)]));

    const icons = fixture.nativeElement.querySelectorAll('.expenses-panel__icon') as NodeListOf<HTMLElement>;
    expect(icons.length).toBe(2);
    icons.forEach((icon) => expect(icon.getAttribute('aria-hidden')).toBe('true'));
  });

  it('shows the empty state when the trip has no expenses (F4.R8, F4.AC10 States)', async () => {
    await renderWith(listOf([]));

    expect(screen.getByText(EMPTY)).toBeTruthy();
    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.getByText('0 expenses')).toBeTruthy();
  });

  it('emits tripGone on a 404 so the trip page shows its own message (F4.R15)', async () => {
    render();
    let gone = 0;
    fixture.componentInstance.tripGone.subscribe(() => gone++);

    http.expectOne('/api/trips/trip-1/expenses').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(gone).toBe(1);
    expect(screen.queryByText(GENERIC_ERROR)).toBeNull();
  });

  it('shows the generic error in the panel when the request fails with a server error', async () => {
    render();

    http.expectOne('/api/trips/trip-1/expenses').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
  });
});
