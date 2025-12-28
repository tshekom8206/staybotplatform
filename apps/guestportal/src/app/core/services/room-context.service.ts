import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class RoomContextService {
  private readonly STORAGE_KEY = 'guestportal_room';
  private _roomNumber$ = new BehaviorSubject<string | null>(null);

  public roomNumber$ = this._roomNumber$.asObservable();

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
}
