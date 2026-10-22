import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { waitFor } from '@testing-library/angular';
import { AuthService } from './core/auth/auth.service';
import { apiInterceptor } from './core/http/api.interceptor';
import { routes } from './app.routes';

const LOGIN_REDIRECT = '/login?reason=login-required';

describe('app routes: /trips is guarded (F1.R15, F1.AC1, F1.AC13)', () => {
  let router: Router;
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    router = TestBed.inject(Router);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('sends a logged-out visit to /trips to /login', async () => {
    const navigation = router.navigateByUrl('/trips');
    await waitFor(() => http.expectOne('/api/auth/me')).then((req) =>
      req.flush({}, { status: 401, statusText: 'Unauthorized' }),
    );
    await navigation;

    expect(router.url).toBe(LOGIN_REDIRECT);
  });

  it('sends a logged-out visit to /trips/123 through the wildcard to /login', async () => {
    const navigation = router.navigateByUrl('/trips/123');
    await waitFor(() => http.expectOne('/api/auth/me')).then((req) =>
      req.flush({}, { status: 401, statusText: 'Unauthorized' }),
    );
    await navigation;

    expect(router.url).toBe(LOGIN_REDIRECT);
  });

  it('sends /trips to /login after logout, without a new /me request', async () => {
    auth.markAuthenticated({ id: 'u1', email: 'a@example.com' });
    await router.navigateByUrl('/trips');
    expect(router.url).toBe('/trips');

    const logout = auth.logout();
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await logout;

    // The header sends the visitor to /login after logout; from there /trips is typed in again.
    await router.navigateByUrl('/login');
    await router.navigateByUrl('/trips');
    http.expectNone('/api/auth/me');
    expect(router.url).toBe(LOGIN_REDIRECT);
  });
});
