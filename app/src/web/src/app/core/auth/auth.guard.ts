import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { LOGIN_REQUIRED_REASON } from '../errors/problem-mapper';

/**
 * Waits for the initial GET /api/auth/me, then lets a signed-in user through.
 * A logged-out visitor goes to /login with a reason that the login page maps
 * to "Please log in to see your trips." (F1.R15).
 */
export const authGuard: CanActivateFn = async (): Promise<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.ensureLoaded();
  if (auth.status() === 'authenticated') {
    return true;
  }
  return router.createUrlTree(['/login'], { queryParams: { reason: LOGIN_REQUIRED_REASON } });
};
