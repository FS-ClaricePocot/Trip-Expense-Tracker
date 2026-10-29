import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
import { SettleUp, SuggestedPayment } from '../settle-up.service';
import { SuggestedPaymentsPanelComponent } from './suggested-payments-panel.component';

const LOADING = 'Loading settle up…';
const SETTLED = 'Everyone is settled up.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const HELP_ONE = 'The fewest payments that bring every balance to zero.';
const HELP_TWO = "Money moves outside the app. Mark a payment as paid once it's been made.";

let fixture: ComponentFixture<SuggestedPaymentsPanelComponent>;
let http: HttpTestingController;

/** Creates the panel for trip-1 and starts its settle-up request. */
function render(): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(SuggestedPaymentsPanelComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets the pending response resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

/** A full settle-up body; the panel only reads the suggestions, the other fields are placeholders. */
function body(suggestions: SuggestedPayment[]): SettleUp {
  return {
    suggestions,
    payments: [],
    paidBackCents: 0,
    owedCents: 5000,
    leftToSettleCents: 5000,
    suggestionCount: suggestions.length,
    percentage: 0,
    totalSpentCents: 5000,
    expenseCount: 1,
  };
}

function suggestion(payerName: string, payeeName: string, amountCents: number): SuggestedPayment {
  return {
    payerPersonId: `id-${payerName}`,
    payerName,
    payeePersonId: `id-${payeeName}`,
    payeeName,
    amountCents,
  };
}

/** Example 1 (product.md, F6.AC1): Jo pays Sam 40.00, then Alex pays Sam 10.00. */
const EXAMPLE_ONE = [suggestion('Jo', 'Sam', 4000), suggestion('Alex', 'Sam', 1000)];

/** Renders the panel and answers its settle-up request with the given body. */
async function renderWith(suggestions: SuggestedPayment[]): Promise<void> {
  render();
  http.expectOne('/api/trips/trip-1/settle-up').flush(body(suggestions));
  await settle();
}

describe('SuggestedPaymentsPanelComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the heading and both helper texts as written (F6.R1)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(screen.getByRole('heading', { name: 'Suggested payments' })).toBeTruthy();
    expect(screen.getByText(HELP_ONE)).toBeTruthy();
    expect(screen.getByText(HELP_TWO)).toBeTruthy();
  });

  it('shows the loading text first, then requests the trip settle-up data', async () => {
    render();

    expect(screen.getByText(LOADING)).toBeTruthy();
    http.expectOne('/api/trips/trip-1/settle-up').flush(body([]));
    await settle();

    expect(screen.queryByText(LOADING)).toBeNull();
  });

  it('lists Example 1 in the order returned: Jo pays Sam 40.00, then Alex pays Sam 10.00 (F6.AC1)', async () => {
    await renderWith(EXAMPLE_ONE);

    const items = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Jo pays Sam');
    expect(items[0].textContent).toContain('Settles Jo\'s balance');
    expect(items[0].textContent).toContain('40.00');
    expect(items[1].textContent).toContain('Alex pays Sam');
    expect(items[1].textContent).toContain('10.00');
  });

  it('names each Mark as paid button with the payer, payee and amount (Accessibility)', async () => {
    await renderWith(EXAMPLE_ONE);

    expect(screen.getByRole('button', { name: 'Mark Jo pays Sam 40.00 as paid' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Mark Alex pays Sam 10.00 as paid' })).toBeTruthy();
  });

  it('emits the suggestion when its Mark as paid button is pressed', async () => {
    await renderWith(EXAMPLE_ONE);
    let emitted: SuggestedPayment | undefined;
    fixture.componentInstance.markPaid.subscribe((value) => (emitted = value));

    screen.getByRole('button', { name: 'Mark Alex pays Sam 10.00 as paid' }).click();

    expect(emitted).toEqual(EXAMPLE_ONE[1]);
  });

  it('shows "Everyone is settled up." when there are no suggestions (F6.R14, F6.AC3)', async () => {
    await renderWith([]);

    expect(screen.getByText(SETTLED)).toBeTruthy();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('shows the error copy in the panel when the request fails', async () => {
    render();
    http.expectOne('/api/trips/trip-1/settle-up').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('emits tripGone on a 404 so the trip page can show its not-found message (F6.R15)', async () => {
    render();
    let gone = 0;
    fixture.componentInstance.tripGone.subscribe(() => gone++);
    http.expectOne('/api/trips/trip-1/settle-up').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(gone).toBe(1);
  });
});
