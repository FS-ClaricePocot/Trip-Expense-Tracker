import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { fireEvent, screen, waitFor, within } from '@testing-library/angular';
import { Person } from '../people.service';
import { PeoplePanelComponent } from './people-panel.component';

const EMPTY = 'No one is on this trip yet. Add the people who will share its expenses.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const REMOVED_SAM = 'Sam was removed from the trip.';
const IN_USE_SAM = "Sam is in an expense or payment, so they can't be removed.";

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

/** Lets pending work resolve, then re-checks the view. */
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

/** Selects the remove control for a person and waits for the dialog to open. */
async function openRemove(name: string): Promise<HTMLElement> {
  fireEvent.click(screen.getByRole('button', { name: `Remove ${name}` }));
  fixture.detectChanges();
  const dialog = await screen.findByRole('dialog');
  return dialog;
}

function removeButtonIn(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole('button', { name: 'Remove' });
}

describe('PeoplePanelComponent removal (F3.T8)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('gives each removable row a control named "Remove {name}" (F3.R6)', async () => {
    await renderWith([person('p1', 'Sam')]);

    expect(screen.getByRole('button', { name: 'Remove Sam' })).toBeTruthy();
  });

  it('shows the tag in place of the remove control for a person in an expense (F3.R7, F3.AC8)', async () => {
    await renderWith([person('p1', 'Sam', true, false)]);

    expect(screen.queryByRole('button', { name: 'Remove Sam' })).toBeNull();
    expect(screen.getByText('In expenses')).toBeTruthy();
  });

  it('shows the in-payments tag in place of the remove control for a person only in a payment (F3.AC9)', async () => {
    await renderWith([person('p1', 'Riley', false, true)]);

    expect(screen.queryByRole('button', { name: 'Remove Riley' })).toBeNull();
    expect(screen.getByText('In payments')).toBeTruthy();
  });

  it('shows the exact dialog title, body and buttons, with Cancel focused first (F3.R6)', async () => {
    await renderWith([person('p1', 'Sam')]);

    const dialog = await openRemove('Sam');

    expect(within(dialog).getByRole('heading', { name: 'Remove Sam?' })).toBeTruthy();
    expect(
      within(dialog).getByText('They will no longer be on this trip. You can add them again later.'),
    ).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Remove' })).toBeTruthy();
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeTruthy();
    await waitFor(() => expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Cancel' })));
  });

  it('closes on Cancel and sends no DELETE, Sam stays (F3.AC7)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const dialog = await openRemove('Sam');

    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    fixture.detectChanges();
    await settle();

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByRole('button', { name: 'Remove Sam' })).toBeTruthy();
    http.expectNone({ method: 'DELETE' });
  });

  it('closes on Escape and returns focus to the remove control (F3.R6)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const control = screen.getByRole('button', { name: 'Remove Sam' });
    control.focus();
    const dialog = await openRemove('Sam');

    fireEvent.keyDown(document.activeElement!, { key: 'Escape', code: 'Escape', keyCode: 27 });
    fixture.detectChanges();

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(control));
    expect(dialog).toBeTruthy();
  });

  it('keeps focus inside while open: Tab and Shift+Tab cycle the dialog buttons, not the page behind (F3.R6)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const dialog = await openRemove('Sam');
    const remove = removeButtonIn(dialog);
    const cancel = within(dialog).getByRole('button', { name: 'Cancel' });
    // The page behind the modal is aria-hidden, so the query must include hidden elements.
    await waitFor(() => expect(document.activeElement).toBe(cancel));
    // CDK places two focus-trap anchors around the dialog. A Tab or Shift+Tab that would leave the dialog
    // lands on the anchor first, and the anchor hands focus back into the dialog.
    const anchors = Array.from(document.querySelectorAll<HTMLElement>('.cdk-focus-trap-anchor'));
    const [startAnchor, endAnchor] = anchors;
    expect(anchors.length).toBe(2);

    // Tab from the last button (Cancel) wraps to the first button (Remove).
    endAnchor.focus();
    await waitFor(() => expect(document.activeElement).toBe(remove));

    // Shift+Tab from the first button (Remove) wraps to the last button (Cancel).
    startAnchor.focus();
    await waitFor(() => expect(document.activeElement).toBe(cancel));
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it('removes Sam on Remove: DELETE is sent, the row goes and the status text shows (F3.AC6)', async () => {
    await renderWith([person('p1', 'Sam'), person('p2', 'Riley')]);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    await settle();

    expect(screen.queryByRole('button', { name: 'Remove Sam' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Remove Riley' })).toBeTruthy();
    expect(screen.getByRole('status').textContent).toContain(REMOVED_SAM);
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'People' })));
  });

  it('shows the in-use message, reloads the list and Sam now has the tag on a 409 (F3.AC8, F3.R7)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(
      { type: 'https://tripexpensetracker/problems/person-in-use', title: 'In use', status: 409 },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();
    expect(screen.queryByRole('dialog')).toBeNull();

    http.expectOne('/api/trips/trip-1/people').flush([person('p1', 'Sam', true, false)]);
    await settle();

    expect(screen.getByText('In expenses')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Remove Sam' })).toBeNull();
    expect(screen.getByRole('alert').textContent).toContain(IN_USE_SAM);
  });

  it('on a 404 from DELETE with a good reload, refreshes the list and sends no trip-gone event', async () => {
    await renderWith([person('p1', 'Sam'), person('p2', 'Riley')]);
    const goneSpy = jasmine.createSpy('tripGone');
    fixture.componentInstance.tripGone.subscribe(goneSpy);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(
      {},
      { status: 404, statusText: 'Not Found' },
    );
    await settle();
    expect(screen.queryByRole('dialog')).toBeNull();

    http.expectOne('/api/trips/trip-1/people').flush([person('p2', 'Riley')]);
    await settle();

    expect(screen.queryByRole('button', { name: 'Remove Sam' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Remove Riley' })).toBeTruthy();
    expect(goneSpy).not.toHaveBeenCalled();
  });

  it('hands over to the trip page when the reload after a 404 is also a 404 (F3.R10)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const goneSpy = jasmine.createSpy('tripGone');
    fixture.componentInstance.tripGone.subscribe(goneSpy);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(
      {},
      { status: 404, statusText: 'Not Found' },
    );
    await settle();

    http.expectOne('/api/trips/trip-1/people').flush({}, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(goneSpy).toHaveBeenCalled();
  });

  it('shows the generic error inside the dialog on a 500 and keeps the dialog open', async () => {
    await renderWith([person('p1', 'Sam')]);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(
      {},
      { status: 500, statusText: 'Server Error' },
    );
    await settle();

    const open = screen.getByRole('dialog');
    expect(within(open).getByRole('alert').textContent).toContain(GENERIC_ERROR);
    expect(within(open).getByRole('button', { name: 'Cancel' })).toBeTruthy();
  });

  it('shows the empty state after the last person is removed (F3.R12)', async () => {
    await renderWith([person('p1', 'Sam')]);
    const dialog = await openRemove('Sam');

    fireEvent.click(removeButtonIn(dialog));
    fixture.detectChanges();
    http.expectOne({ method: 'DELETE', url: '/api/trips/trip-1/people/p1' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    await settle();

    expect(screen.getByText(EMPTY)).toBeTruthy();
  });

  it('has no control to rename a person (F3.R8, F3.AC11)', async () => {
    await renderWith([person('p1', 'Sam')]);

    expect(screen.queryByRole('button', { name: /rename|edit/i })).toBeNull();
    expect(screen.queryByRole('textbox', { name: 'Sam' })).toBeNull();
  });
});
