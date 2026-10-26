import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { fireEvent, screen, waitFor, within } from '@testing-library/angular';
import { TripListComponent } from '../trip-list/trip-list.component';
import { TripPageComponent } from './trip-page.component';

const NOT_FOUND = 'This trip no longer exists.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const DIALOG_BODY = "This deletes the trip and everything in it. You can't undo this.";
const TRIP = { id: 'trip-1', name: 'Paris 2026', createdAt: '2026-10-01T10:00:00Z', peopleCount: 0, expenseCount: 0, paymentCount: 0 };

/** Opens /trips/trip-1 through the real router, so the trip list route also exists for the delete flow. */
async function openTripPage(): Promise<{ harness: RouterTestingHarness; http: HttpTestingController }> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'trips', component: TripListComponent },
        { path: 'trips/:tripId', component: TripPageComponent },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });
  const harness = await RouterTestingHarness.create('/trips/trip-1');
  return { harness, http: TestBed.inject(HttpTestingController) };
}

/** Lets pending promises, the dialog and focus updates run, then re-checks the view. */
async function settle(harness: RouterTestingHarness): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 20));
  harness.detectChanges();
  // F3.T6 stub: the People panel asks for the trip's people once the trip has loaded. Answer it with an empty list.
  TestBed.inject(HttpTestingController).match('/api/trips/trip-1/people').forEach((request) => request.flush([]));
}

/** Focuses the Delete button, as a click would, then opens the dialog and returns it. */
async function openDialog(harness: RouterTestingHarness): Promise<HTMLElement> {
  const deleteButton = screen.getByRole('button', { name: 'Delete trip Paris 2026' });
  deleteButton.focus();
  fireEvent.click(deleteButton);
  await settle(harness);
  return screen.getByRole('dialog');
}

describe('TripPageComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the trip name and a Delete button named for the trip (F2.AC8)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);

    expect(screen.getByRole('heading', { name: 'Paris 2026' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Delete trip Paris 2026' })).toBeTruthy();
  });

  it('has no control to rename or edit the trip (F2.R8, F2.AC13)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);

    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('button', { name: /edit|rename/i })).toBeNull();
  });

  it('shows the not-found message and a link to the list for a 404 (F2.R9, F2.AC6, F2.AC11)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(null, { status: 404, statusText: 'Not Found' });
    await settle(harness);

    expect(screen.getByRole('alert').textContent?.trim()).toBe(NOT_FOUND);
    expect(screen.getByRole('link', { name: 'Your trips' }).getAttribute('href')).toBe('/trips');
    expect(screen.queryByRole('button', { name: /Delete trip/ })).toBeNull();
  });

  it('shows the generic error when the trip request fails with a server error', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(harness);

    expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR);
  });

  it('shows the title, body and the three counts in the delete dialog (F2.R7)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);

    const dialog = await openDialog(harness);

    expect(dialog.getAttribute('aria-labelledby')).toBeTruthy();
    expect(screen.getByRole('dialog', { name: 'Delete Paris 2026?' })).toBeTruthy();
    expect(within(dialog).getByText(DIALOG_BODY)).toBeTruthy();
    expect(within(dialog).getByText('0 people')).toBeTruthy();
    expect(within(dialog).getByText('0 expenses')).toBeTruthy();
    expect(within(dialog).getByText('0 payment')).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Delete trip' })).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeTruthy();
  });

  it('puts initial focus on Cancel and keeps focus inside the dialog (F2.R7)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);

    const dialog = await openDialog(harness);

    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Cancel' }));

    // Moving focus to the trap's end sentinel must bring it back inside the dialog.
    const anchors = document.querySelectorAll<HTMLElement>('.cdk-focus-trap-anchor');
    anchors[anchors.length - 1].focus();
    await settle(harness);
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it('closes on Cancel, sends no DELETE and keeps the trip (F2.AC9)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);
    const dialog = await openDialog(harness);

    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    http.expectNone({ method: 'DELETE' });
    expect(screen.getByRole('heading', { name: 'Paris 2026' })).toBeTruthy();
  });

  it('closes on Escape and returns focus to the Delete button (F2.R7)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);
    await openDialog(harness);

    // A browser sends both key and keyCode; CDK checks keyCode.
    fireEvent.keyDown(document.activeElement!, { key: 'Escape', code: 'Escape', keyCode: 27 });

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Delete trip Paris 2026' })));
    http.expectNone({ method: 'DELETE' });
  });

  it('confirms the delete, sends DELETE, goes to the list and shows "Trip deleted." (F2.AC10)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);
    const dialog = await openDialog(harness);

    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete trip' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1' }).flush(null, { status: 204, statusText: 'No Content' });

    await waitFor(() => http.expectOne('/api/trips')).then((req) => req.flush([]));
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/trips');
    expect(screen.getByRole('status').textContent?.trim()).toBe('Trip deleted.');
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('shows the not-found message inside the dialog when the delete returns 404 (F2.AC11)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);
    const dialog = await openDialog(harness);

    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete trip' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1' }).flush(null, { status: 404, statusText: 'Not Found' });

    await waitFor(() => expect(within(dialog).getByRole('alert').textContent?.trim()).toBe(NOT_FOUND));
  });

  it('shows the generic error inside the dialog when the delete fails with 500 (F2 States)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);
    const dialog = await openDialog(harness);

    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete trip' }));
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1' }).flush(null, { status: 500, statusText: 'Server Error' });

    await waitFor(() => expect(within(dialog).getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR));
    expect(screen.getByRole('dialog')).toBeTruthy();
  });

  it('shows the not-found message once when the people request returns 404 (F3.R10)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await new Promise((resolve) => setTimeout(resolve, 20));
    harness.detectChanges();
    http.expectOne('/api/trips/trip-1/people').flush(null, { status: 404, statusText: 'Not Found' });
    await settle(harness);

    expect(screen.getAllByText(NOT_FOUND).length).toBe(1);
    expect(screen.getAllByRole('alert').length).toBe(1);
    expect(screen.queryByRole('heading', { name: 'Paris 2026' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'People' })).toBeNull();
  });

  it('shows the new people count in the delete dialog after a person is removed (F3.R14, F2.R7)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush({ ...TRIP, peopleCount: 1 });
    await new Promise((resolve) => setTimeout(resolve, 20));
    harness.detectChanges();
    http.expectOne('/api/trips/trip-1/people').flush([{ id: 'p1', name: 'Sam', addedAt: '2026-10-01T10:00:00Z', inExpense: false, inPayment: false }]);
    await settle(harness);

    fireEvent.click(screen.getByRole('button', { name: 'Remove Sam' }));
    harness.detectChanges();
    const removeDialog = await screen.findByRole('dialog');
    fireEvent.click(within(removeDialog).getByRole('button', { name: 'Remove' }));
    harness.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(null, { status: 204, statusText: 'No Content' });
    await settle(harness);

    const dialog = await openDialog(harness);

    expect(within(dialog).getByText('0 people')).toBeTruthy();
    expect(within(dialog).queryByText('1 person')).toBeNull();
  });

  it('shows a "Your trips" back link above the title on a loaded trip (mockup)', async () => {
    const { harness, http } = await openTripPage();
    http.expectOne('/api/trips/trip-1').flush(TRIP);
    await settle(harness);

    const link = screen.getByRole('link', { name: 'Your trips' });

    expect(link.getAttribute('href')).toBe('/trips');
    expect(link.compareDocumentPosition(screen.getByRole('heading', { name: 'Paris 2026' })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
