import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Booking,
  CreateBookingRequest,
  UpdateBookingRequest,
  BookingStatistics,
  BookingFilter
} from '../models/guest.model';

export interface BookingListResponse {
  data: Booking[];
  pagination: {
    currentPage: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

export interface AvailableRoomsResponse {
  rooms: string[];
  count: number;
}

export interface RoomAvailabilityResponse {
  roomNumber: string;
  isAvailable: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class BookingService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Get bookings with filtering and pagination
   */
  getBookings(filter?: BookingFilter): Observable<BookingListResponse> {
    let params = new HttpParams();

    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }
    if (filter?.status) {
      params = params.set('status', filter.status);
    }
    if (filter?.search) {
      params = params.set('search', filter.search);
    }
    if (filter?.checkinFrom) {
      params = params.set('checkinFrom', filter.checkinFrom);
    }
    if (filter?.checkinTo) {
      params = params.set('checkinTo', filter.checkinTo);
    }
    if (filter?.checkoutFrom) {
      params = params.set('checkoutFrom', filter.checkoutFrom);
    }
    if (filter?.checkoutTo) {
      params = params.set('checkoutTo', filter.checkoutTo);
    }
    if (filter?.source) {
      params = params.set('source', filter.source);
    }

    return this.http.get<BookingListResponse>(`${this.baseUrl}/bookings`, { params });
  }

  /**
   * Get a single booking by ID
   */
  getBookingById(id: number): Observable<Booking> {
    return this.http.get<Booking>(`${this.baseUrl}/bookings/${id}`);
  }

  /**
   * Create a new booking
   */
  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(`${this.baseUrl}/bookings`, request);
  }

  /**
   * Update an existing booking
   */
  updateBooking(id: number, request: UpdateBookingRequest): Observable<Booking> {
    return this.http.put<Booking>(`${this.baseUrl}/bookings/${id}`, request);
  }

  /**
   * Cancel a booking
   */
  cancelBooking(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/bookings/${id}`);
  }

  /**
   * Get booking statistics
   */
  getStatistics(): Observable<BookingStatistics> {
    return this.http.get<BookingStatistics>(`${this.baseUrl}/bookings/statistics`);
  }

  /**
   * Get available rooms for a date range
   */
  getAvailableRooms(checkinDate: string, checkoutDate: string, excludeBookingId?: number): Observable<AvailableRoomsResponse> {
    let params = new HttpParams()
      .set('checkinDate', checkinDate)
      .set('checkoutDate', checkoutDate);

    if (excludeBookingId) {
      params = params.set('excludeBookingId', excludeBookingId.toString());
    }

    return this.http.get<AvailableRoomsResponse>(`${this.baseUrl}/bookings/available-rooms`, { params });
  }

  /**
   * Check if a specific room is available
   */
  checkRoomAvailability(roomNumber: string, checkinDate: string, checkoutDate: string, excludeBookingId?: number): Observable<RoomAvailabilityResponse> {
    let params = new HttpParams()
      .set('roomNumber', roomNumber)
      .set('checkinDate', checkinDate)
      .set('checkoutDate', checkoutDate);

    if (excludeBookingId) {
      params = params.set('excludeBookingId', excludeBookingId.toString());
    }

    return this.http.get<RoomAvailabilityResponse>(`${this.baseUrl}/bookings/check-availability`, { params });
  }
}
