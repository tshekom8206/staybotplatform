import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TenantService } from './tenant.service';

export interface RoomValidationResult {
  valid: boolean;
  roomNumber?: string;
  error?: string;
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RoomContextService {
  private readonly STORAGE_KEY = 'guestportal_room';
  private _roomNumber$ = new BehaviorSubject<string | null>(null);
  private http = inject(HttpClient);
  private tenantService = inject(TenantService);

  public roomNumber$ = this._roomNumber$.asObservable();

  private get apiUrl(): string {
    const slug = this.tenantService.getTenantSlug();
    return `${environment.apiUrl}/api/public/${slug}`;
  }

  constructor() {
    this.loadFromStorage();
    this.loadFromUrl();
  }

  /**
   * Load room number from localStorage
   */
  private loadFromStorage(): void {
    const stored = localStorage.getItem(this.STORAGE_KEY);
    if (stored) {
      this._roomNumber$.next(stored);
    }
  }

  /**
   * Load room number from URL query param
   * e.g., ?room=205
   */
  private loadFromUrl(): void {
    const params = new URLSearchParams(window.location.search);
    const room = params.get('room');
    if (room) {
      this.setRoomNumber(room);
    }
  }

  /**
   * Set room number and persist to localStorage
   */
  setRoomNumber(room: string): void {
    this._roomNumber$.next(room);
    localStorage.setItem(this.STORAGE_KEY, room);
  }

  /**
   * Get current room number synchronously
   */
  getRoomNumber(): string | null {
    return this._roomNumber$.getValue();
  }

  /**
   * Clear room number
   */
  clearRoomNumber(): void {
    this._roomNumber$.next(null);
    localStorage.removeItem(this.STORAGE_KEY);
  }

  /**
   * Check if room number is set
   */
  hasRoomNumber(): boolean {
    return this._roomNumber$.getValue() !== null;
  }

  /**
   * Validate room number against the property's valid rooms list
   */
  async validateRoom(roomNumber: string): Promise<RoomValidationResult> {
    try {
      const result = await firstValueFrom(
        this.http.get<RoomValidationResult>(`${this.apiUrl}/rooms/validate/${encodeURIComponent(roomNumber.trim())}`)
      );
      return result;
    } catch (error: any) {
      console.error('Error validating room:', error);
      return {
        valid: false,
        error: error?.error?.error || 'Failed to validate room number'
      };
    }
  }

  /**
   * Validate and set room number - only saves if valid
   */
  async setRoomNumberWithValidation(roomNumber: string): Promise<RoomValidationResult> {
    const result = await this.validateRoom(roomNumber);

    if (result.valid) {
      this.setRoomNumber(result.roomNumber || roomNumber.trim());
    }

    return result;
  }
}
