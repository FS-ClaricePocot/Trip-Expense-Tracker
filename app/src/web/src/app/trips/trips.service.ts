import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** One item of GET /api/trips (F2.R5). The list is already newest first. */
export interface TripSummary {
  id: string;
  name: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class TripsService {
  private readonly http = inject(HttpClient);

  list(): Promise<TripSummary[]> {
    return firstValueFrom(this.http.get<TripSummary[]>('/api/trips'));
  }
}
