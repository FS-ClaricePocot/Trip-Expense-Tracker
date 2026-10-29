import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, waitFor, within } from '@testing-library/angular';
import { RecordedPayment } from '../settle-up.service';
import { PaymentsMadeListComponent } from './payments-made-list.component';

const EMPTY = 'No payments yet. Payments you mark as paid will appear here.';
const DELETED = 'Payment deleted.';
const MISSING = 'This payment no longer exists.';

/**
 * Noon UTC on 20 Oct 2026. The row shows it in the machine's zone, which can be 19, 20 or 21 Oct, so the row test
 * checks only the format. The exact day in a fixed zone is tested in date-format.spec.ts.
 */
const PAY_JO_SAM: RecordedPayment = {
  id: 'pay-2',
  payerPersonId: 'p-jo',
  payerName: 'Jo',
  payeePersonId: 'p-sam',
  payeeName: 'Sam',
  amountCents: 4000,
  markedPaidAt: '2026-10-20T12:00:00Z',
};

const PAY_ALEX_SAM: RecordedPayment = {
  id: 'pay-1',
  payerPersonId: 'p-alex',
  payerName: 'Alex',
  payeePersonId: 'p-sam',
  payeeName: 'Sam',
  amountCents: 1000,
  markedPaidAt: '2026-10-19T12:00:00Z',
};

let fixture: ComponentFixture<PaymentsMadeListComponent>;
let http: HttpTestingController;

/** Creates the list for trip-1 with the given payments (newest first, as the API returns them). */
function render(payments: RecordedPayment[]): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(PaymentsMadeListComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  fixture.componentRef.setInput('payments', payments);
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets pending work resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

/** Opens the delete dialog for the named row's control and waits for it. */
async function openDelete(name: string): Promise<void> {
  screen.getByRole('button', { name }).click();
  await waitFor(() => expect(screen.getByRole('dialog')).toBeTruthy());
}

describe('PaymentsMadeListComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows "Jo paid Sam", 40.00 and "Marked paid · <day> Oct 2026" in any time zone (F6.AC5, F6 spec Edge cases)', () => {
    render([PAY_JO_SAM]);

    const row = within(screen.getByRole('list')).getAllByRole('listitem')[0];
    expect(row.textContent).toContain('Jo paid Sam');
    expect(row.textContent).toContain('40.00');
    expect(row.textContent).toMatch(/Marked paid · (19|20|21) Oct 2026/);
  });

  it('lists the payments newest first, in the order given (F6.R7, F6.AC15)', () => {
    render([PAY_JO_SAM, PAY_ALEX_SAM]);

    const items = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Jo paid Sam');
    expect(items[1].textContent).toContain('Alex paid Sam');
  });

  it('names each delete control with the payer, payee and amount (Accessibility)', () => {
    render([PAY_JO_SAM]);

    expect(screen.getByRole('button', { name: 'Delete Jo paid Sam 40.00' })).toBeTruthy();
  });

  it('has no edit control on a payment row (F6.AC11, F6.R9)', () => {
    render([PAY_JO_SAM]);

    const row = within(screen.getByRole('list')).getAllByRole('listitem')[0];
    expect(within(row).queryByRole('button', { name: /edit/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /edit/i })).toBeNull();
  });

  it('shows the empty state copy when there are no payments', () => {
    render([]);

    expect(screen.getByText(EMPTY)).toBeTruthy();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('shows "1 payment" in the heading count for one payment (F6 spec, Plural forms)', () => {
    render([PAY_JO_SAM]);

    expect(screen.getByRole('heading', { name: 'Payments made' })).toBeTruthy();
    expect(screen.getByText('1 payment')).toBeTruthy();
  });

  it('shows "2 payments" in the heading count for two payments (F6 spec, Plural forms)', () => {
    render([PAY_JO_SAM, PAY_ALEX_SAM]);

    expect(screen.getByText('2 payments')).toBeTruthy();
  });

  it('Cancel in the delete dialog sends nothing and keeps the payment (F6.AC6)', async () => {
    render([PAY_JO_SAM]);
    await openDelete('Delete Jo paid Sam 40.00');

    screen.getByRole('button', { name: 'Cancel' }).click();
    await settle();

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByRole('button', { name: 'Delete Jo paid Sam 40.00' })).toBeTruthy();
    http.expectNone({ method: 'DELETE' });
  });

  it('deletes on confirm: DELETE is sent and "Payment deleted." shows (F6.AC6)', async () => {
    render([PAY_JO_SAM]);
    let refreshed = 0;
    fixture.componentInstance.refresh.subscribe(() => refreshed++);
    await openDelete('Delete Jo paid Sam 40.00');

    screen.getByRole('button', { name: 'Delete payment' }).click();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/payments/pay-2' }).flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    await waitFor(() => expect(screen.getByRole('status').textContent?.trim()).toBe(DELETED));
    // The view reloads the rows through the service's change event, so the list asks for nothing more.
    expect(refreshed).toBe(0);
  });

  it('asks the view to reload when the payment is already gone: "This payment no longer exists." shows (F6.R15)', async () => {
    render([PAY_JO_SAM]);
    let refreshed = 0;
    fixture.componentInstance.refresh.subscribe(() => refreshed++);
    await openDelete('Delete Jo paid Sam 40.00');

    screen.getByRole('button', { name: 'Delete payment' }).click();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/payments/pay-2' }).flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    await waitFor(() => expect(screen.getByRole('status').textContent?.trim()).toBe(MISSING));
    expect(refreshed).toBe(1);
  });
});
