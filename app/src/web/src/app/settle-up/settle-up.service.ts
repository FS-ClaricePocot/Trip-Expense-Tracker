import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Subject, firstValueFrom, tap } from 'rxjs';

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

/** The body of POST /api/trips/{tripId}/payments: the suggestion the user saw, in whole cents (F6.R18). */
export interface NewPayment {
  payerPersonId: string;
  payeePersonId: string;
  amountCents: number;
}

@Injectable({ providedIn: 'root' })
export class SettleUpService {
  private readonly http = inject(HttpClient);
  private readonly changed = new Subject<string>();

  /**
   * Emits the trip id after this app records or deletes a payment there, so the suggestions, tiles
   * and balances reload (F6.AC5, F6.AC6).
   */
  readonly paymentsChanged = this.changed.asObservable();

  /** GET /api/trips/{tripId}/settle-up. A 404 means the trip is not the user's or does not exist (F6.R15). */
  get(tripId: string): Promise<SettleUp> {
    return firstValueFrom(this.http.get<SettleUp>(`/api/trips/${encodeURIComponent(tripId)}/settle-up`));
  }

  /**
   * POST /api/trips/{tripId}/payments (F6.R5, F6.R18). A 409 means the suggestions changed, so nothing
   * was recorded. The server returns 201 with the saved payment; the dialog does not need it.
   */
  markPaid(tripId: string, payment: NewPayment): Promise<void> {
    return firstValueFrom(
      this.http
        .post<void>(`/api/trips/${encodeURIComponent(tripId)}/payments`, payment)
        .pipe(tap(() => this.changed.next(tripId))),
    );
  }

  /** DELETE /api/trips/{tripId}/payments/{paymentId} (F6.R8). The server returns 204. */
  deletePayment(tripId: string, paymentId: string): Promise<void> {
    return firstValueFrom(
      this.http
        .delete<void>(`/api/trips/${encodeURIComponent(tripId)}/payments/${encodeURIComponent(paymentId)}`)
        .pipe(tap(() => this.changed.next(tripId))),
    );
  }
}
