import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { apiInterceptor } from './api.interceptor';

describe('apiInterceptor', () => {
  let http: HttpClient;
  let testing: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    testing = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => testing.verify());

  it('sends credentials on API calls', () => {
    http.get('/api/trips').subscribe({ error: () => undefined });
    const req = testing.expectOne('/api/trips');

    expect(req.request.withCredentials).toBeTrue();
    req.flush([]);
  });

  it('does not change credentials on calls outside /api', () => {
    http.get('/assets/config.json').subscribe();
    const req = testing.expectOne('/assets/config.json');

    expect(req.request.withCredentials).toBeFalse();
    req.flush({});
  });

  it('on a 401 from a non-auth endpoint, clears the signed-in state and goes to /login', () => {
    auth.markAuthenticated({ id: 'u1', email: 'a@example.com' });
    const navigate = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    let errorStatus: number | undefined;

    http.get('/api/trips').subscribe({
      error: (e: HttpErrorResponse) => (errorStatus = e.status),
    });
    testing.expectOne('/api/trips').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(errorStatus).toBe(401);
    expect(auth.status()).toBe('anonymous');
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('on a 401 from /api/auth/me, does not navigate', () => {
    const navigate = spyOn(router, 'navigateByUrl').and.resolveTo(true);

    http.get('/api/auth/me').subscribe({ error: () => undefined });
    testing.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});
