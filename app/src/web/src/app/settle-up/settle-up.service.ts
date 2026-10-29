import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** One suggested payment in GET /api/trips/{tripId}/settle-up (F6.R1). Amounts are whole cents. */
export interface SuggestedPayment {
  payerPersonId: string;
  payerName: string;
  payeePersonId: string;
  payeeName: string;
  amountCents: number;
}

/** One recorded payment in the settle-up response, newest first (F6.R7). */
export interface RecordedPayment {
  id: string;
  payerPersonId: string;
  payerName: string;
  payeePersonId: string;
  payeeName: string;
  amountCents: number;
  markedPaidAt: string;
}

/** The whole GET /api/trips/{tripId}/settle-up response (F6 Contract changes). */
export interface SettleUp {
  suggestions: SuggestedPayment[];
  payments: RecordedPayment[];
  paidBackCents: number;
  owedCents: number;
  leftToSettleCents: number;
  suggestionCount: number;
  /** Null when nothing is owed (F6.AC14). */
  percentage: number | null;
  totalSpentCents: number;
  expenseCount: number;
}

@Injectable({ providedIn: 'root' })
export class SettleUpService {
  private readonly http = inject(HttpClient);

  /** GET /api/trips/{tripId}/settle-up. A 404 means the trip is not the user's or does not exist (F6.R15). */
  get(tripId: string): Promise<SettleUp> {
    return firstValueFrom(this.http.get<SettleUp>(`/api/trips/${encodeURIComponent(tripId)}/settle-up`));
  }
}
