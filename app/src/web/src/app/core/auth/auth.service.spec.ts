import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { apiInterceptor } from '../http/api.interceptor';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts as unknown with no user', () => {
    expect(service.status()).toBe('unknown');
    expect(service.user()).toBeNull();
  });

  it('moves from unknown to anonymous on a 401 from GET /api/auth/me', async () => {
    const loaded = service.ensureLoaded();
    const req = http.expectOne('/api/auth/me');
    expect(req.request.withCredentials).toBeTrue();
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    await loaded;

    expect(service.status()).toBe('anonymous');
    expect(service.user()).toBeNull();
  });

  it('moves to authenticated and stores the user on a 200 from /api/auth/me', async () => {
    const loaded = service.ensureLoaded();
    http.expectOne('/api/auth/me').flush({ id: 'u1', email: 'a@example.com' });
    await loaded;

    expect(service.status()).toBe('authenticated');
    expect(service.user()).toEqual({ id: 'u1', email: 'a@example.com' });
  });

  it('sends the /me request only once for repeated ensureLoaded calls', async () => {
    const first = service.ensureLoaded();
    const second = service.ensureLoaded();
    http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' });
    await Promise.all([first, second]);

    await service.ensureLoaded();
    http.expectNone('/api/auth/me');
    expect(service.status()).toBe('anonymous');
  });

  it('markAnonymous clears a signed-in user', () => {
    service.markAuthenticated({ id: 'u1', email: 'a@example.com' });
    service.markAnonymous();

    expect(service.status()).toBe('anonymous');
    expect(service.user()).toBeNull();
  });
});
