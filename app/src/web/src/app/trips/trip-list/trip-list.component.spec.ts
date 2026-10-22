import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { render, screen, waitFor } from '@testing-library/angular';
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
    const items = screen.getAllByRole('listitem').map((item) => item.textContent?.trim());
    expect(items).toEqual(['Lisbon', 'Porto', 'Paris']);
  });

  it('makes each trip name a link to its trip page (F2.R5)', async () => {
    const http = await renderList();
    http.expectOne('/api/trips').flush([{ id: 'abc-123', name: 'Lisbon', createdAt: '2026-10-03T10:00:00Z' }]);

    await waitFor(() => expect(screen.getByRole('link', { name: 'Lisbon' })).toBeTruthy());
    expect(screen.getByRole('link', { name: 'Lisbon' }).getAttribute('href')).toBe('/trips/abc-123');
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
});
