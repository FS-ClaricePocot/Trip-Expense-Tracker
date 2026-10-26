import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { fireEvent, render, screen } from '@testing-library/angular';
import { AuthService } from '../auth/auth.service';
import { apiInterceptor } from '../http/api.interceptor';
import { HeaderComponent } from './header.component';

async function renderHeader(): Promise<{ http: HttpTestingController; router: Router; auth: AuthService }> {
  await render(HeaderComponent, {
    providers: [
      // A stub /login route, so the test sees the real navigation (router.url).
      provideRouter([{ path: 'login', children: [] }]),
      provideHttpClient(withInterceptors([apiInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  return {
    http: TestBed.inject(HttpTestingController),
    router: TestBed.inject(Router),
    auth: TestBed.inject(AuthService),
  };
}

function clickLogOut(): void {
  fireEvent.click(screen.getByRole('button', { name: 'Log out' }));
}

describe('HeaderComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows the Log out button', async () => {
    await renderHeader();

    expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  });

  it('calls POST /api/auth/logout, clears the signed-in state and goes to /login (F1.R5, F1.AC6)', async () => {
    const { http, router, auth } = await renderHeader();
    auth.markAuthenticated({ id: 'u1', email: 'a@example.com' });

    clickLogOut();
    const req = http.expectOne('/api/auth/logout');
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBeTrue();
    req.flush(null, { status: 204, statusText: 'No Content' });

    await TestBed.inject(ApplicationRef).whenStable();
    expect(auth.status()).toBe('anonymous');
    expect(router.url).toBe('/login');
    expect(auth.user()).toBeNull();
  });

  it('still clears the state and goes to /login when the logout request fails', async () => {
    const { http, router, auth } = await renderHeader();
    auth.markAuthenticated({ id: 'u1', email: 'a@example.com' });

    clickLogOut();
    http.expectOne('/api/auth/logout').flush({}, { status: 500, statusText: 'Server Error' });

    await TestBed.inject(ApplicationRef).whenStable();
    expect(auth.status()).toBe('anonymous');
    expect(router.url).toBe('/login');
    expect(auth.user()).toBeNull();
  });
});
