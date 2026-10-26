import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

const API_PREFIX = '/api/';
const AUTH_PREFIX = '/api/auth/';

/**
 * Sends cookies on every API call (withCredentials), and on a 401 from a
 * non-auth endpoint clears the signed-in state and goes to /login.
 * /api/auth/* is excluded: GET /api/auth/me returns 401 for a visitor who is
 * logged out, and that is an answer, not an expired session.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_PREFIX)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const isAuthEndpoint = req.url.startsWith(AUTH_PREFIX);
  const apiReq = req.clone({ withCredentials: true });

  return next(apiReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthEndpoint) {
        auth.markAnonymous();
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
