import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard } from './auth.guard';
import { apiInterceptor } from '../http/api.interceptor';

describe('authGuard', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let router: Router;

  const runGuard = (): Promise<unknown> =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<unknown>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('redirects a logged-out visitor to /login with the login-required reason', async () => {
    const result = runGuard();
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    const tree = await result;

    expect(router.serializeUrl(tree as ReturnType<Router['createUrlTree']>)).toBe('/login?reason=login-required');
  });

  it('allows an authenticated user through', async () => {
    const result = runGuard();
    http.expectOne('/api/auth/me').flush({ id: 'u1', email: 'a@example.com' });

    expect(await result).toBeTrue();
    expect(auth.status()).toBe('authenticated');
  });

  it('does not send a second /me request when state is already known', async () => {
    auth.markAuthenticated({ id: 'u1', email: 'a@example.com' });

    expect(await runGuard()).toBeTrue();
    http.expectNone('/api/auth/me');
  });
});
