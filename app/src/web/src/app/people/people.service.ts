import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * One item of GET /api/trips/{tripId}/people (F3.R12), in the order added.
 * `inExpense` and `inPayment` drive the tag in place of the remove control (F3.R7).
 */
export interface Person {
  id: string;
  name: string;
  addedAt: string;
  inExpense: boolean;
  inPayment: boolean;
}

@Injectable({ providedIn: 'root' })
export class PeopleService {
  private readonly http = inject(HttpClient);

  list(tripId: string): Promise<Person[]> {
    return firstValueFrom(this.http.get<Person[]>(`/api/trips/${encodeURIComponent(tripId)}/people`));
  }

  /** POST /api/trips/{tripId}/people (F3.R1). The server returns the added person (201). */
  add(tripId: string, name: string): Promise<Person> {
    return firstValueFrom(this.http.post<Person>(`/api/trips/${encodeURIComponent(tripId)}/people`, { name }));
  }

  /** DELETE /api/trips/{tripId}/people/{personId} (F3.R6). 204 on success; 409 when in use (F3.R7). */
  remove(tripId: string, personId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`/api/trips/${encodeURIComponent(tripId)}/people/${encodeURIComponent(personId)}`),
    );
  }
}
