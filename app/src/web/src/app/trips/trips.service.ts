import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** One item of GET /api/trips (F2.R5). The list is already newest first. */
export interface TripSummary {
  id: string;
  name: string;
  createdAt: string;
}

/** F2.R4: a user can have at most this many trips. */
export const TRIP_LIMIT = 50;

/** Body of POST /api/trips (F2.R1, F2.R3). */
export interface CreateTripRequest {
  name: string;
  confirmDuplicate?: boolean;
}

@Injectable({ providedIn: 'root' })
export class TripsService {
  private readonly http = inject(HttpClient);

  list(): Promise<TripSummary[]> {
    return firstValueFrom(this.http.get<TripSummary[]>('/api/trips'));
  }

  create(request: CreateTripRequest): Promise<TripSummary> {
    return firstValueFrom(this.http.post<TripSummary>('/api/trips', request));
  }
}
