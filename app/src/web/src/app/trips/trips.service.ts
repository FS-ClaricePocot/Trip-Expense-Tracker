import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** One item of GET /api/trips (F2.R5), and a created trip (F3.R13, F4.R17, F4.R18). The list is already newest first. */
export interface TripSummary {
  id: string;
  name: string;
  createdAt: string;
  peopleCount: number;
  expenseCount: number;
  totalCents: number;
}

/** F2.R4: a user can have at most this many trips. */
export const TRIP_LIMIT = 50;

/** Body of POST /api/trips (F2.R1, F2.R3). */
export interface CreateTripRequest {
  name: string;
  confirmDuplicate?: boolean;
}

/** Response of GET /api/trips/{id} (F2.R6, F2.R7). The counts are 0 until F3 to F6. */
export interface TripDetail extends TripSummary {
  peopleCount: number;
  expenseCount: number;
  paymentCount: number;
}

/** Router navigation state key that tells the trip list to show "Trip deleted." (F2.R7). */
export const TRIP_DELETED_STATE = 'tripDeleted';

@Injectable({ providedIn: 'root' })
export class TripsService {
  private readonly http = inject(HttpClient);

  list(): Promise<TripSummary[]> {
    return firstValueFrom(this.http.get<TripSummary[]>('/api/trips'));
  }

  create(request: CreateTripRequest): Promise<TripSummary> {
    return firstValueFrom(this.http.post<TripSummary>('/api/trips', request));
  }

  get(id: string): Promise<TripDetail> {
    return firstValueFrom(this.http.get<TripDetail>(`/api/trips/${encodeURIComponent(id)}`));
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/trips/${encodeURIComponent(id)}`));
  }
}
