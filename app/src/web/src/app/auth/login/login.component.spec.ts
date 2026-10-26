import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { AuthService } from '../../core/auth/auth.service';
import { LoginComponent } from './login.component';

const INVALID_CREDENTIALS = 'Email or password is incorrect.';
const GENERIC_ERROR = 'Something went wrong. Please try again.';
const LOGIN_REQUIRED = 'Please log in to see your trips.';
const LOGGING_IN = 'Logging in…';
const EMAIL_REQUIRED = 'Enter your email address.';
const PASSWORD_REQUIRED = 'Enter your password.';

async function renderPage(query: Record<string, string> = {}): Promise<{
  http: HttpTestingController;
  router: Router;
  auth: AuthService;
}> {
  const { fixture } = await render(LoginComponent, {
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } },
    ],
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
  fireEvent.submit(screen.getByRole('button', { name: 'Log in' }).closest('form')!);
}

function alertText(): string {
  return screen.getByRole('alert').textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('LoginComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the heading, labels, copy, register link and privacy line', async () => {
    await renderPage();

    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeTruthy();
    expect(screen.getByText('Log in to pick up where your trips left off.')).toBeTruthy();
    expect(screen.getByText('Your trips are private. Only you can see them.')).toBeTruthy();
    expect(screen.getByText('Split the trip,')).toBeTruthy();
    expect(screen.getByLabelText('Email')).toBeTruthy();
    expect(screen.getByLabelText('Password')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href')).toBe('/register');
    expect(screen.getByRole('button', { name: 'Log in' })).toBeTruthy();
  });

  // F1.R8 and F1.AC5: a wrong password and an unknown email get the same 401 and the same copy.
  // The two tests below send the same request shape and check the same copy.
  it('shows the generic message for a wrong password', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'wrong-password');
    submitForm();

    http.expectOne('/api/auth/login').flush(
      { type: 'invalid-credentials', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );

    await waitFor(() => expect(alertText()).toBe(INVALID_CREDENTIALS));
  });

  it('shows the same generic message for an unknown email, with no hint that the account is missing', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'nobody@x.com');
    typeInto('Password', 'any-password-1');
    submitForm();

    http.expectOne('/api/auth/login').flush(
      { type: 'invalid-credentials', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );

    await waitFor(() => expect(alertText()).toBe(INVALID_CREDENTIALS));
    expect(document.body.textContent).not.toMatch(/not registered|no account|doesn't exist|does not exist/i);
  });

  it('shows the redirect message when the guard sent the user here', async () => {
    await renderPage({ reason: 'login-required' });

    expect(alertText()).toBe(LOGIN_REQUIRED);
  });

  it('shows no alert when the page is opened directly', async () => {
    await renderPage();

    // An empty .form-alert is hidden (styles.scss: .form-alert:empty), so no alert role is exposed.
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows the generic message on a 500', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    submitForm();

    http.expectOne('/api/auth/login').flush(
      { title: 'Server error', status: 500 },
      { status: 500, statusText: 'Internal Server Error' },
    );

    await waitFor(() => expect(alertText()).toBe(GENERIC_ERROR));
  });

  it('shows the generic message when the network request fails', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    submitForm();

    http.expectOne('/api/auth/login').error(new ProgressEvent('error'), { status: 0, statusText: '' });

    await waitFor(() => expect(alertText()).toBe(GENERIC_ERROR));
  });

  it('disables the submit button and shows the sending text while the request runs', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    submitForm();

    const button = screen.getByRole('button', { name: LOGGING_IN }) as HTMLButtonElement;
    expect(button.disabled).toBeTrue();

    http.expectOne('/api/auth/login').flush(
      { type: 'invalid-credentials' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await waitFor(() => expect(screen.getByRole('button', { name: 'Log in' })).toBeTruthy());
    expect((screen.getByRole('button', { name: 'Log in' }) as HTMLButtonElement).disabled).toBeFalse();
  });

  it('sends the trimmed, lower-cased email', async () => {
    const { http } = await renderPage();
    typeInto('Email', '  Anna@X.com ');
    typeInto('Password', 'any-password-1');
    submitForm();

    const req = http.expectOne('/api/auth/login');
    expect(req.request.body).toEqual({ email: 'anna@x.com', password: 'any-password-1' });
    req.flush({ id: 'u1', email: 'anna@x.com' }, { status: 200, statusText: 'OK' });
  });

  it('goes to /trips on success and marks the visitor signed in', async () => {
    const { http, router, auth } = await renderPage({ returnUrl: '/trips/123' });
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    submitForm();

    http.expectOne('/api/auth/login').flush({ id: 'u1', email: 'anna@x.com' }, { status: 200, statusText: 'OK' });

    await waitFor(() => expect(router.navigateByUrl).toHaveBeenCalledWith('/trips'));
    expect(router.navigateByUrl).toHaveBeenCalledTimes(1);
    expect(auth.status()).toBe('authenticated');
  });

  // Client-side checks: an empty field is caught before any request is sent.
  it('shows the email message for an empty email and sends no request', async () => {
    const { http } = await renderPage();
    typeInto('Password', 'any-password-1');
    submitForm();

    await waitFor(() => expect(screen.getByText(EMAIL_REQUIRED)).toBeTruthy());
    http.expectNone('/api/auth/login');
    const email = screen.getByLabelText('Email');
    expect(email.getAttribute('aria-invalid')).toBe('true');
    expect(email.getAttribute('aria-describedby')).toBe('email-error');
  });

  it('shows the password message for an empty password and sends no request', async () => {
    const { http } = await renderPage();
    typeInto('Email', 'anna@x.com');
    submitForm();

    await waitFor(() => expect(screen.getByText(PASSWORD_REQUIRED)).toBeTruthy());
    http.expectNone('/api/auth/login');
    const password = screen.getByLabelText('Password');
    expect(password.getAttribute('aria-invalid')).toBe('true');
    expect(password.getAttribute('aria-describedby')).toBe('password-error');
  });

  it('treats a whitespace-only email as empty and sends no request', async () => {
    const { http } = await renderPage();
    typeInto('Email', '   ');
    typeInto('Password', 'any-password-1');
    submitForm();

    await waitFor(() => expect(screen.getByText(EMAIL_REQUIRED)).toBeTruthy());
    http.expectNone('/api/auth/login');
  });

  it('sends the request once the empty fields are filled, and clears the messages', async () => {
    const { http, router } = await renderPage();
    submitForm();
    await waitFor(() => expect(screen.getByText(EMAIL_REQUIRED)).toBeTruthy());
    http.expectNone('/api/auth/login');

    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    await waitFor(() => expect(screen.queryByText(EMAIL_REQUIRED)).toBeNull());
    expect(screen.queryByText(PASSWORD_REQUIRED)).toBeNull();
    submitForm();

    http.expectOne('/api/auth/login').flush({ id: 'u1', email: 'anna@x.com' }, { status: 200, statusText: 'OK' });
    await waitFor(() => expect(router.navigateByUrl).toHaveBeenCalledWith('/trips'));
  });

  it('can be completed with the keyboard alone, in order', async () => {
    const { http, router } = await renderPage();

    // Tab order: the create-account link, then email, password and the submit button.
    const form = screen.getByRole('button', { name: 'Log in' }).closest('form')!;
    const page = screen.getByRole('main');
    const focusable = Array.from(page.querySelectorAll<HTMLElement>('a[href], input, button')).filter(
      (el) => el.tabIndex >= 0 && !(el as HTMLButtonElement).disabled,
    );
    const names = focusable.map((el) => el.id || (el.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(names).toEqual(['Create an account', 'email', 'password', 'Log in →']);

    for (const el of focusable) {
      el.focus();
      expect(document.activeElement).toBe(el);
    }

    screen.getByLabelText('Email').focus();
    typeInto('Email', 'anna@x.com');
    typeInto('Password', 'any-password-1');
    fireEvent.submit(form);

    http.expectOne('/api/auth/login').flush({ id: 'u1', email: 'anna@x.com' }, { status: 200, statusText: 'OK' });
    await waitFor(() => expect(router.navigateByUrl).toHaveBeenCalledWith('/trips'));
  });
});
