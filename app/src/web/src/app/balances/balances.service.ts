import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The status the API returns; the front end picks the words (F5.R6). */
export type BalanceStatus = 'owed' | 'owes' | 'settled';

/** One person in GET /api/trips/{tripId}/balances (F5.R4). Amounts are whole cents. */
export interface PersonBalance {
  id: string;
  name: string;
  balanceCents: number;
  status: BalanceStatus;
}

/** The whole balances response, in seq order. */
export interface BalanceList {
  totalCents: number;
  people: PersonBalance[];
}

/** The trip summary tiles in GET /api/trips/{tripId}/summary (F5.R8). Amounts are whole cents. */
export interface TripSummary {
  totalSpentCents: number;
  leftToSettleCents: number;
}

@Injectable({ providedIn: 'root' })
export class BalancesService {
  private readonly http = inject(HttpClient);

  /** GET /api/trips/{tripId}/balances. A 500 with type "balances_inconsistent" means the check failed (F5.R3). */
  get(tripId: string): Promise<BalanceList> {
    return firstValueFrom(this.http.get<BalanceList>(`/api/trips/${encodeURIComponent(tripId)}/balances`));
  }

  /** GET /api/trips/{tripId}/summary: Total spent and Left to settle (F5.R8). */
  summary(tripId: string): Promise<TripSummary> {
    return firstValueFrom(this.http.get<TripSummary>(`/api/trips/${encodeURIComponent(tripId)}/summary`));
  }
}
