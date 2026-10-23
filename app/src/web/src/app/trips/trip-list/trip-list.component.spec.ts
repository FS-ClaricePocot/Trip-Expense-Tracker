import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { TripListComponent } from './trip-list.component';

const LOADING = 'Loading your trips…';
const EMPTY = 'You have no trips yet. Create your first trip to start tracking shared expenses.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';

async function renderList(): Promise<HttpTestingController> {
  await render(TripListComponent, {
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  return TestBed.inject(HttpTestingController);
}

describe('TripListComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the heading and the loading text first (F2.R5)', async () => {
    const http = await renderList();

    expect(screen.getByRole('heading', { name: 'Your trips' })).toBeTruthy();
    expect(screen.getByText(LOADING)).toBeTruthy();
    http.expectOne('/api/trips').flush([]);
  });

  it('shows the empty state and no list when there are no trips (F2.R11, F2.AC12)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([]);

    await waitFor(() => expect(screen.getByText(EMPTY)).toBeTruthy());
    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.queryByText(LOADING)).toBeNull();
  });

  it('renders three trips as list items in the order the API returns them (F2.R5)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([
      { id: 'id-3', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z' },
      { id: 'id-2', name: 'Porto', createdAt: '2026-10-02T10:00:00Z' },
      { id: 'id-1', name: 'Paris', createdAt: '2026-10-01T10:00:00Z' },
    ]);

    await waitFor(() => expect(screen.getByRole('list')).toBeTruthy());
    const items = screen.getAllByRole('listitem').map((item) => item.querySelector('.trip-card__name')?.textContent?.trim());
    expect(items).toEqual(['Lisbon', 'Porto', 'Paris']);
  });

  it('makes each trip name a link to its trip page (F2.R5)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'abc-123', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z' }]);

    await waitFor(() => expect(screen.getByRole('link', { name: 'Lisbon' })).toBeTruthy());
    expect(screen.getByRole('link', { name: 'Lisbon' }).getAttribute('href')).toBe('/trips/abc-123');
  });

  it('shows "3 people" on a card with three people (F3.R13, F3.AC15)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'id-1', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z', peopleCount: 3 }]);

    await waitFor(() => expect(screen.getByText('3 people')).toBeTruthy());
  });

  it('shows "1 person" on a card with one person (F3.R13, F3.AC16)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'id-1', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z', peopleCount: 1 }]);

    await waitFor(() => expect(screen.getByText('1 person')).toBeTruthy());
  });

  it('shows "No people yet" on a card with no people (F3.R13, F3.AC16)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'id-1', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z', peopleCount: 0 }]);

    await waitFor(() => expect(screen.getByText('No people yet')).toBeTruthy());
  });

  it('shows the generic error when the list request fails (F2 States)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush(null, { status: 500, statusText: 'Server Error' });

    await waitFor(() => expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR));
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('shows the Log out button from the shared header (F1.R16)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([]);

    expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  });

  it('puts a trip created with Create anyway at the top of the list (F2.AC1, F2.AC4)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'id-1', name: 'Paris 2026', createdAt: '2026-10-01T10:00:00Z' }]);
    await waitFor(() => expect(screen.getByRole('list')).toBeTruthy());

    fireEvent.input(screen.getByLabelText('New trip'), { target: { value: 'paris 2026' } });
    fireEvent.submit(screen.getByRole('button', { name: 'Create trip' }).closest('form')!);
    http.expectOne('/api/trips').flush(
      { type: 'duplicate-trip-name', title: 'Duplicate', status: 409 },
      { status: 409, statusText: 'Conflict' },
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Create anyway' }));
    http.expectOne('/api/trips').flush({ id: 'id-2', name: 'paris 2026', createdAt: '2026-10-22T10:00:00Z' }, {
      status: 201,
      statusText: 'Created',
    });

    await settle();
    expect(
      screen.getAllByRole('listitem').map((item) => item.querySelector('.trip-card__name')?.textContent?.trim()),
    ).toEqual(['paris 2026', 'Paris 2026']);
  });

  it('disables the create form and shows the limit text at 50 trips (F2.R4, F2.AC5)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush(fiftyTrips());

    await waitFor(() =>
      expect(screen.getByText('You have reached the limit of 50 trips. Delete a trip to create a new one.')).toBeTruthy(),
    );
    expect((screen.getByLabelText('New trip') as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Create trip' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('reloads the list when the server refuses a create at the limit (F2.R4)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([]);
    await waitFor(() => expect(screen.getByText(EMPTY)).toBeTruthy());

    fireEvent.input(screen.getByLabelText('New trip'), { target: { value: 'Lisbon' } });
    fireEvent.submit(screen.getByRole('button', { name: 'Create trip' }).closest('form')!);
    http.expectOne('/api/trips').flush(
      { type: 'trip-limit-reached', title: 'Limit', status: 409 },
      { status: 409, statusText: 'Conflict' },
    );
    // The reload is requested after the create promise settles, so let that happen first.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne('/api/trips').flush(fiftyTrips());

    await waitFor(() =>
      expect(screen.getByText('You have reached the limit of 50 trips. Delete a trip to create a new one.')).toBeTruthy(),
    );
    expect(screen.getAllByRole('listitem').length).toBe(50);
  });
});

/** Lets the promise chain after a flushed response finish before the DOM is checked. */
function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 50));
}

function fiftyTrips(): { id: string; name: string; createdAt: string }[] {
  return Array.from({ length: 50 }, (_, index) => ({
    id: `id-${index}`,
    name: `Trip ${index}`,
    createdAt: '2026-10-01T10:00:00Z',
  }));
}
