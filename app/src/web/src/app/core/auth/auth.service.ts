import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type AuthStatus = 'unknown' | 'anonymous' | 'authenticated';

export interface CurrentUser {
  id: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly statusSignal = signal<AuthStatus>('unknown');
  private readonly userSignal = signal<CurrentUser | null>(null);
  private initialLoad: Promise<void> | null = null;

  readonly status = this.statusSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();

  /**
   * Resolves once the initial GET /api/auth/me has finished. Only the first
   * call sends the request; later calls reuse it.
   */
  ensureLoaded(): Promise<void> {
    if (this.statusSignal() !== 'unknown') {
      return Promise.resolve();
    }
    this.initialLoad ??= this.loadMe();
    return this.initialLoad;
  }

  /** Clears the signed-in state. Called on a 401 from a non-auth endpoint. */
  markAnonymous(): void {
    this.userSignal.set(null);
    this.statusSignal.set('anonymous');
  }

  /** Sets the signed-in state. Used by register and log in (later tasks). */
  markAuthenticated(user: CurrentUser): void {
    this.userSignal.set(user);
    this.statusSignal.set('authenticated');
  }

  private async loadMe(): Promise<void> {
    try {
      const user = await firstValueFrom(this.http.get<CurrentUser>('/api/auth/me'));
      this.markAuthenticated(user);
    } catch {
      // A 401 means no valid session. Any other failure also leaves the
      // visitor logged out, so the guard can redirect instead of hanging.
      this.markAnonymous();
    }
  }
}
