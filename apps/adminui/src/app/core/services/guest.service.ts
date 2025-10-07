import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Guest, Conversation, Booking, ConversationFilter, GuestStatus, BookingStatus, ConversationStatus } from '../models/guest.model';

export interface GuestConversationSummary {
  id: number;
  guestId: number;
  guestName: string;
  phoneNumber: string;
  roomNumber?: string;
  lastMessage: string;
  lastMessageAt: Date | string;
  status: ConversationStatus;
  priority: 'low' | 'medium' | 'high' | 'urgent';
  unreadCount: number;
  conversationId: number;
}

export interface CheckinSummary {
  id: number;
  guestName: string;
  phoneNumber: string;
  roomNumber: string;
  expectedArrival: Date;
  actualArrival?: Date;
  status: 'pending' | 'in-progress' | 'completed' | 'no-show';
  specialRequests?: string;
  numberOfGuests: number;
}

export interface GuestHistorySummary {
  id: number;
  date: Date;
  guestName: string;
  roomNumber: string;
  interactionType: 'chat' | 'task' | 'emergency' | 'complaint' | 'request';
  staffMember: string;
  summary: string;
  status: 'resolved' | 'pending' | 'escalated';
  duration?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GuestService {
  private http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Get active conversations with guest details
   */
  getActiveConversations(): Observable<GuestConversationSummary[]> {
    return this.http.get<GuestConversationSummary[]>(`${this.baseUrl}/guest/conversations/active`);
  }

  /**
   * Get today's check-ins
   */
  getTodayCheckins(): Observable<CheckinSummary[]> {
    const today = new Date();
    const params = new HttpParams().set('date', today.toISOString().split('T')[0]);
    return this.http.get<{data: CheckinSummary[]}>(`${this.baseUrl}/checkin`, { params })
      .pipe(
        map((response: {data: CheckinSummary[]}) => response.data || [])
      );
  }

  /**
   * Get guest interaction history
   */
  getGuestHistory(page: number = 1, pageSize: number = 20, filters?: any): Observable<GuestHistorySummary[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<GuestHistorySummary[]>(`${this.baseUrl}/guest/history`, { params });
  }

  /**
   * Get conversation details
   */
  getConversation(conversationId: number): Observable<Conversation> {
    return this.http.get<Conversation>(`${this.baseUrl}/conversations/${conversationId}`);
  }

  /**
   * Send message to guest
   */
  sendMessage(phoneNumber: string, message: string, conversationId?: number): Observable<any> {
    const payload = {
      phoneNumber,
      message,
      conversationId
    };
    return this.http.post(`${this.baseUrl}/guest/messages/send`, payload);
  }

  /**
   * Update conversation status
   */
  updateConversationStatus(conversationId: number, status: ConversationStatus): Observable<any> {
    return this.http.patch(`${this.baseUrl}/guest/conversations/${conversationId}/status`, { status });
  }

  /**
   * Update check-in status
   */
  updateCheckinStatus(checkinId: number, status: string): Observable<any> {
    return this.http.patch(`${this.baseUrl}/checkins/${checkinId}/status`, { status });
  }

  /**
   * Get guest details
   */
  getGuestDetails(guestId: number): Observable<Guest> {
    return this.http.get<Guest>(`${this.baseUrl}/guest/${guestId}`);
  }

  /**
   * Search guests
   */
  searchGuests(query: string): Observable<Guest[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<Guest[]>(`${this.baseUrl}/guests/search`, { params });
  }
}