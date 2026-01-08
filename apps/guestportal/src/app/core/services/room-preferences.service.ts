import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { TenantService } from './tenant.service';
import { RoomContextService } from './room-context.service';

export interface RoomPreference {
  id: number;
  preferenceType: string;
  preferenceValue: any;
  notes?: string;
  status: string;
  acknowledgedAt?: string;
  acknowledgedByName?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateRoomPreferenceRequest {
  preferenceType: string;
  preferenceValue: any;
  notes?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RoomPreferencesService {
  private http = inject(HttpClient);
  private tenantService = inject(TenantService);
  private roomContext = inject(RoomContextService);

  private get apiUrl(): string {
    const slug = this.tenantService.getTenantSlug();
    return `${environment.apiUrl}/api/public/${slug}/housekeeping-preferences`;
  }

  getPreferences(roomNumber?: string, status?: string): Observable<RoomPreference[]> {
    let url = this.apiUrl;
    const params: string[] = [];

    const room = roomNumber || this.roomContext.getRoomNumber();
    if (room) params.push(`roomNumber=${room}`);
    if (status) params.push(`status=${status}`);

    if (params.length > 0) {
      url += `?${params.join('&')}`;
    }

    return this.http.get<RoomPreference[]>(url).pipe(
      catchError(error => {
        console.error('Error fetching preferences:', error);
        return of([]);
      })
    );
  }

  createOrUpdatePreference(request: CreateRoomPreferenceRequest): Observable<RoomPreference> {
    const roomNumber = this.roomContext.getRoomNumber() || 'Unknown';
    const payload = {
      roomNumber,
      preferenceType: request.preferenceType,
      preferenceValue: request.preferenceValue,
      notes: request.notes
    };

    return this.http.post<RoomPreference>(this.apiUrl, payload);
  }

  cancelPreference(id: number): Observable<void> {
    // For now, we don't support cancelling via the public API
    // Return an observable that completes immediately
    return of(undefined);
  }
}
