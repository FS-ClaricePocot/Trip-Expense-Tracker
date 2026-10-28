import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
import { SummaryTilesComponent } from './summary-tiles.component';

const GENERIC_ERROR = 'Something went wrong. Please try again.';

let fixture: ComponentFixture<SummaryTilesComponent>;
let http: HttpTestingController;

/** Creates the tiles for trip-1 and starts their summary request. */
function render(): void {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  fixture = TestBed.createComponent(SummaryTilesComponent);
  fixture.componentRef.setInput('tripId', 'trip-1');
  http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
}

/** Lets the pending response resolve, then re-checks the view. */
async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  fixture.detectChanges();
}

describe('SummaryTilesComponent', () => {
  afterEach(() => http.verify());

  it('shows Total spent 120.00 and Left to settle 50.00 for Example 1 (F5.AC9, F5.AC10)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/summary').flush({ totalSpentCents: 12000, leftToSettleCents: 5000 });
    await settle();

    expect(within(screen.getByRole('heading', { name: /Total spent/ })).getByText('120.00')).toBeTruthy();
    expect(within(screen.getByRole('heading', { name: /Left to settle/ })).getByText('50.00')).toBeTruthy();
  });

  it('shows the Total spent tile before the Left to settle tile (F5.R8)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/summary').flush({ totalSpentCents: 12000, leftToSettleCents: 5000 });
    await settle();

    const headings = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent ?? '');
    expect(headings.length).toBe(2);
    expect(headings[0]).toContain('Total spent');
    expect(headings[1]).toContain('Left to settle');
  });

  it('shows the tiles as headings with the amounts in their text (accessibility)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/summary').flush({ totalSpentCents: 184250, leftToSettleCents: 0 });
    await settle();

    const total = screen.getByRole('heading', { name: /Total spent/ });
    expect(total.textContent).toContain('1,842.50');
    expect(screen.getByRole('heading', { name: /Left to settle/ }).textContent).toContain('0.00');
  });

  it('has no Biggest payer tile (F5 spec, Out)', async () => {
    render();
    http.expectOne('/api/trips/trip-1/summary').flush({ totalSpentCents: 12000, leftToSettleCents: 5000 });
    await settle();

    expect(screen.queryByText(/Biggest payer/)).toBeNull();
  });

  it('tells the page the trip is gone once for a 404 and shows no tiles (F5.R12)', async () => {
    render();
    const gone = jasmine.createSpy('tripGone');
    fixture.componentInstance.tripGone.subscribe(gone);
    http.expectOne('/api/trips/trip-1/summary').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(gone).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('heading')).toBeNull();
  });

  it('shows the generic error when the summary request fails', async () => {
    render();
    http.expectOne('/api/trips/trip-1/summary').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
    expect(screen.queryByRole('heading')).toBeNull();
  });
});
