import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { AuthService } from '../../core/auth/auth.service';
import { RegisterComponent } from './register.component';

const EMAIL_REQUIRED = 'Enter your email address.';
const EMAIL_INVALID = 'Enter an email address like name@example.com.';
const PASSWORD_TOO_SHORT = 'Your password must be at least 8 characters.';
const PASSWORD_TOO_LONG = 'Your password can be at most 128 characters.';
const REGISTRATION_FAILED = "We couldn't create your account with these details.";
const CREATING = 'Creating your account…';

async function renderPage(): Promise<{ http: HttpTestingController; router: Router; auth: AuthService }> {
  const { fixture } = await render(RegisterComponent, {
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const http = TestBed.inject(HttpTestingController);
  const router = fixture.debugElement.injector.get(Router);
  const auth = fixture.debugElement.injector.get(AuthService);
  spyOn(router, 'navigateByUrl').and.resolveTo(true);
  return { http, router, auth };
}

function typeInto(label: string, value: string): void {
  fireEvent.input(screen.getByLabelText(label), { target: { value } });
}

function submitForm(): void {
  fireEvent.submit(screen.getByRole('button', { name: /create account/i }).closest('form')!);
}

describe('RegisterComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the heading, labels, copy and log-in link', async () => {
    await renderPage();

    expect(screen.getByRole('heading', { name: 'Create your account' })).toBeTruthy();
    expect(screen.getByText('Just an email and a password.')).toBeTruthy();
    expect(screen.getByText('Your trips are private. Only you can see them.')).toBeTruthy();
    expect(screen.getByLabelText('Email')).toBeTruthy();
    expect(screen.getByLabelText('Password')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
    expect(screen.getByRole('button', { name: 'Create account' })).toBeTruthy();
  });

  it('shows the email required message for an empty email', async () => {
    await renderPage();
    typeInto('Password', 'a-good-password');
    submitForm();

    expect(screen.getByText(EMAIL_REQUIRED)).toBeTruthy();
    expect(screen.getByLabelText('Email').getAttribute('aria-describedby')).toBe('email-error');
  });

  it('shows the email format message for anna.x.com', async () => {
    await renderPage();
    typeInto('Email', 'anna.x.com');
    typeInto('Password', 'a-good-password');
    submitForm();

    expect(screen.getByText(EMAIL_INVALID)).toBeTruthy();
  });

  it('shows the password too short message for 7 characters', async () => {
    await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '1234567');
    submitForm();

    expect(screen.getByText(PASSWORD_TOO_SHORT)).toBeTruthy();
    expect(screen.getByLabelText('Password').getAttribute('aria-describedby')).toBe('password-error');
  });

  it('shows the password too long message for 129 characters', async () => {
    await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'a'.repeat(129));
    submitForm();

    expect(screen.getByText(PASSWORD_TOO_LONG)).toBeTruthy();
  });

  it('accepts a password of 8 characters and sends the request', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '12345678');
    submitForm();

    const req = http.expectOne('/api/auth/register');
    expect(req.request.body).toEqual({ email: 'anna@x.com', password: '12345678' });
    req.flush({ id: 'u1', email: 'anna@x.com' }, { status: 201, statusText: 'Created' });
  });

  it('accepts a password of 128 characters and sends the request', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'a'.repeat(128));
    submitForm();

    const req = http.expectOne('/api/auth/register');
    expect((req.request.body as { password: string }).password.length).toBe(128);
    req.flush({ id: 'u1', email: 'anna@x.com' }, { status: 201, statusText: 'Created' });
  });

  it('trims and lower-cases the email before sending it', async () => {
    const { http } = await renderPage();
    typeInto('Email', '  Anna@X.com ');
    typeInto('Password', '12345678');
    submitForm();

    const req = http.expectOne('/api/auth/register');
    expect((req.request.body as { email: string }).email).toBe('anna@x.com');
    req.flush({ id: 'u1', email: 'anna@x.com' }, { status: 201, statusText: 'Created' });
  });

  it('disables the submit button and shows the sending text while the request runs', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '12345678');
    submitForm();

    const button = screen.getByRole('button', { name: CREATING }) as HTMLButtonElement;
    expect(button.disabled).toBeTrue();

    http.expectOne('/api/auth/register').flush(
      { type: 'registration-failed' },
      { status: 400, statusText: 'Bad Request' },
    );
    await waitFor(() => expect(screen.getByRole('button', { name: 'Create account' })).toBeTruthy());
    expect((screen.getByRole('button', { name: 'Create account' }) as HTMLButtonElement).disabled).toBeFalse();
  });

  it('shows the generic message on a 400 and no text says the email is registered', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '12345678');
    submitForm();

    http.expectOne('/api/auth/register').flush(
      { type: 'registration-failed', status: 400 },
      { status: 400, statusText: 'Bad Request' },
    );

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain(REGISTRATION_FAILED));
    expect(document.body.textContent).not.toMatch(/already (registered|exists)|is registered|exists/i);
  });

  it('navigates to /trips on success', async () => {
    const { http, router } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '12345678');
    submitForm();

    http.expectOne('/api/auth/register').flush(
      { id: 'u1', email: 'anna@x.com' },
      { status: 201, statusText: 'Created' },
    );

    await waitFor(() => expect(router.navigateByUrl).toHaveBeenCalledWith('/trips'));
  });

  it('can be completed with the keyboard alone', async () => {
    const { http, router, auth } = await renderPage();

    // Tab order: the log-in link, then email, password and the submit button.
    const form = screen.getByRole('button', { name: 'Create account' }).closest('form')!;
    const page = screen.getByRole('main');
    const focusable = Array.from(page.querySelectorAll<HTMLElement>('a[href], input, button')).filter(
      (el) => el.tabIndex >= 0 && !(el as HTMLButtonElement).disabled,
    );
    const names = focusable.map((el) => el.id || (el.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(names).toEqual(['Log in', 'email', 'password', 'Create account →']);

    // Each control takes keyboard focus.
    for (const el of focusable) {
      el.focus();
      expect(document.activeElement).toBe(el);
    }

    // Enter in a text field submits the form; keyboard users reach it the same way.
    screen.getByLabelText('Email').focus();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', '12345678');
    fireEvent.submit(form);

    http.expectOne('/api/auth/register').flush(
      { id: 'u1', email: 'anna@x.com' },
      { status: 201, statusText: 'Created' },
    );
    await waitFor(() => expect(auth.status()).toBe('authenticated'));
    await waitFor(() => expect(router.navigateByUrl).toHaveBeenCalledWith('/trips'));
  });
});
