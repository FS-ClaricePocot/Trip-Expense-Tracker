import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** One expense in GET /api/trips/{tripId}/expenses (F4.R8, F4.R10). Amounts are whole cents. */
export interface Expense {
  id: string;
  description: string;
  amountCents: number;
  payer: { id: string; name: string };
  participantCount: number;
  participants: { id: string; name: string; shareCents: number }[];
  createdAt: string;
}

/** The whole list response (F4.R13): newest first, with the total of every amount. */
export interface ExpenseList {
  total: number;
  count: number;
  expenses: Expense[];
}

/** The body of POST /api/trips/{tripId}/expenses (F4.R1). `amount` is the text as typed; the server parses it. */
export interface NewExpense {
  description: string;
  amount: string;
  payerPersonId: string;
  participantPersonIds: string[];
}

@Injectable({ providedIn: 'root' })
export class ExpensesService {
  private readonly http = inject(HttpClient);

  list(tripId: string): Promise<ExpenseList> {
    return firstValueFrom(this.http.get<ExpenseList>(`/api/trips/${encodeURIComponent(tripId)}/expenses`));
  }

  /** POST /api/trips/{tripId}/expenses. The server returns the saved expense (201). */
  add(tripId: string, expense: NewExpense): Promise<Expense> {
    return firstValueFrom(this.http.post<Expense>(`/api/trips/${encodeURIComponent(tripId)}/expenses`, expense));
  }
}
