import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { TripCreateFormComponent } from './trip-create-form.component';

const NAME_REQUIRED = 'Enter a trip name.';
const NAME_TOO_LONG = 'A trip name can be at most 100 characters.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const LIMIT = 'You have reached the limit of 50 trips. Delete a trip to create a new one.';
const CREATING = 'Creating trip…';
const CREATED = { id: 'id-1', name: 'Paris 2026', createdAt: '2026-10-22T10:00:00Z' };

async function renderForm() {
  const { fixture } = await render(TripCreateFormComponent, {
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  return { fixture, http: TestBed.inject(HttpTestingController) };
}

/** Lets the promise chain after a flushed response finish before the DOM is checked. */
function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 50));
}

function typeName(value: string): void {
  fireEvent.input(screen.getByLabelText('New trip'), { target: { value } });
}

function submit(): void {
  fireEvent.submit(screen.getByRole('button', { name: /^Create trip$|^Creating trip…$/ }).closest('form')!);
}

function nameInput(): HTMLInputElement {
  return screen.getByLabelText('New trip') as HTMLInputElement;
}

function duplicateProblem(): object {
  return { type: 'duplicate-trip-name', title: 'Duplicate', status: 409 };
}

describe('TripCreateFormComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the visible label and the Create trip button (F2.R1)', async () => {
    await renderForm();

    expect(nameInput()).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create trip' })).toBeTruthy();
  });

  it('refuses a blank name with the exact copy and sends nothing (F2.R2, F2.AC2)', async () => {
    const { http } = await renderForm();
    typeName('   ');
    submit();

    await waitFor(() => expect(screen.getByText(NAME_REQUIRED)).toBeTruthy());
    http.expectNone('/api/trips');
  });

  it('refuses a name of 101 characters with the exact copy and sends nothing (F2.R2, F2.AC3)', async () => {
    const { http } = await renderForm();
    typeName('a'.repeat(101));
    submit();

    await waitFor(() => expect(screen.getByText(NAME_TOO_LONG)).toBeTruthy());
    http.expectNone('/api/trips');
  });

  it('sends a name of 100 characters (F2.R2, F2.AC3)', async () => {
    const { http } = await renderForm();
    typeName('a'.repeat(100));
    submit();

    const request = http.expectOne('/api/trips');
    expect(request.request.body).toEqual({ name: 'a'.repeat(100) });
    request.flush(CREATED, { status: 201, statusText: 'Created' });
  });

  it('shows the duplicate warning with the typed name (F2.R3, F2.AC4)', async () => {
    const { http } = await renderForm();
    typeName('paris 2026');
    submit();
    http.expectOne('/api/trips').flush(duplicateProblem(), { status: 409, statusText: 'Conflict' });

    const warning = await screen.findByRole('alert');
    expect(warning.textContent).toContain(
      "You already have a trip called 'paris 2026'. Create another one with the same name?",
    );
    expect(screen.getByRole('button', { name: 'Create anyway' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeTruthy();
  });

  it('Cancel hides the warning, sends nothing and keeps the typed text (F2.R3, F2.AC4)', async () => {
    const { http } = await renderForm();
    typeName('paris 2026');
    submit();
    http.expectOne('/api/trips').flush(duplicateProblem(), { status: 409, statusText: 'Conflict' });
    await screen.findByRole('alert');

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    http.expectNone('/api/trips');
    expect(nameInput().value).toBe('paris 2026');
  });

  it('Create anyway resends with confirmDuplicate true and clears the field (F2.R3, F2.AC4)', async () => {
    const { fixture, http } = await renderForm();
    const created = jasmine.createSpy('created');
    fixture.componentInstance.created.subscribe(created);
    typeName('paris 2026');
    submit();
    http.expectOne('/api/trips').flush(duplicateProblem(), { status: 409, statusText: 'Conflict' });
    await screen.findByRole('alert');

    fireEvent.click(screen.getByRole('button', { name: 'Create anyway' }));

    const request = http.expectOne('/api/trips');
    expect(request.request.body).toEqual({ name: 'paris 2026', confirmDuplicate: true });
    request.flush(CREATED, { status: 201, statusText: 'Created' });

    await settle();
    expect(created).toHaveBeenCalledWith(CREATED);
    expect(nameInput().value).toBe('');
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('disables the form and shows the limit message on a limit 409 (F2.R4, F2.AC5)', async () => {
    const { fixture, http } = await renderForm();
    const limitReached = jasmine.createSpy('limitReached');
    fixture.componentInstance.tripLimitReached.subscribe(limitReached);
    typeName('Lisbon');
    submit();
    http.expectOne('/api/trips').flush(
      { type: 'trip-limit-reached', title: 'Limit', status: 409 },
      { status: 409, statusText: 'Conflict' },
    );

    await waitFor(() => expect(screen.getByRole('alert').textContent?.trim()).toBe(LIMIT));
    expect(limitReached).toHaveBeenCalled();
    expect(nameInput().disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Create trip' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('disables the button while the request is sent (F2 States, Creating)', async () => {
    const { http } = await renderForm();
    typeName('Lisbon');
    submit();

    const button = screen.getByRole('button', { name: CREATING }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    http.expectOne('/api/trips').flush(CREATED, { status: 201, statusText: 'Created' });
  });

  it('shows the generic error above the form on a 500 (F2 States, Network or server error)', async () => {
    const { http } = await renderForm();
    typeName('Lisbon');
    submit();
    http.expectOne('/api/trips').flush(null, { status: 500, statusText: 'Server Error' });

    await waitFor(() => expect(screen.getByRole('alert').textContent?.trim()).toBe(GENERIC_ERROR));
  });

  it('links the field to the warning so it is announced (F2 Accessibility)', async () => {
    const { http } = await renderForm();
    typeName('paris 2026');
    submit();
    http.expectOne('/api/trips').flush(duplicateProblem(), { status: 409, statusText: 'Conflict' });

    const alert = await screen.findByRole('alert');
    expect(alert.id).toBe('trip-message');
    expect(nameInput().getAttribute('aria-describedby')).toContain('trip-message');
  });

  it('links the field to its error message (F2 Accessibility)', async () => {
    const { http } = await renderForm();
    typeName('');
    submit();

    await waitFor(() => expect(nameInput().getAttribute('aria-describedby')).toContain('trip-name-error'));
    expect(screen.getByText(NAME_REQUIRED)).toBeTruthy();
    http.expectNone('/api/trips');
  });
});
