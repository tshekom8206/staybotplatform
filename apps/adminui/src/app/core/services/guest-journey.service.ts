import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface GuestJourneySettings {
  // Pre-Arrival
  preArrivalEnabled: boolean;
  preArrivalDaysBefore: number;
  preArrivalTime: string;
  preArrivalTemplate?: string;

  // Check-in Day
  checkinDayEnabled: boolean;
  checkinDayTime: string;
  checkinDayTemplate?: string;

  // Welcome Settled
  welcomeSettledEnabled: boolean;
  welcomeSettledHoursAfter: number;
  welcomeSettledTemplate?: string;

  // Mid-Stay
  midStayEnabled: boolean;
  midStayTime: string;
  midStayTemplate?: string;

  // Pre-Checkout
  preCheckoutEnabled: boolean;
  preCheckoutTime: string;
  preCheckoutTemplate?: string;

  // Post-Stay
  postStayEnabled: boolean;
  postStayTime: string;
  postStayTemplate?: string;

  // Media & Other
  welcomeImageUrl?: string;
  includePhotoInWelcome: boolean;
  timezone?: string;
}

export interface TemplatePlaceholder {
  name: string;
  description: string;
}

export interface ScheduledMessage {
  id: number;
  phone: string;
  messageType: string;
  scheduledFor: string;
  status: string;
  sentAt?: string;
  content: string;
  errorMessage?: string;
  retryCount: number;
  guestName: string;
  roomNumber?: string;
}

export interface ScheduledMessagesResponse {
  messages: ScheduledMessage[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({
  providedIn: 'root'
})
export class GuestJourneyService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/api/guest-journey`;

  /**
   * Get proactive message settings for the current tenant
   */
  getSettings(): Observable<GuestJourneySettings> {
    return this.http.get<GuestJourneySettings>(`${this.apiUrl}/settings`);
  }

  /**
   * Update proactive message settings
   */
  updateSettings(settings: GuestJourneySettings): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/settings`, settings);
  }

  /**
   * Get available placeholders for message templates
   */
  getPlaceholders(): Observable<TemplatePlaceholder[]> {
    return this.http.get<TemplatePlaceholder[]>(`${this.apiUrl}/placeholders`);
  }

  /**
   * Preview a template with sample data
   */
  previewTemplate(template: string): Observable<{ preview: string }> {
    return this.http.post<{ preview: string }>(`${this.apiUrl}/preview`, { template });
  }

  /**
   * Get scheduled messages with pagination and filters
   */
  getScheduledMessages(
    page: number = 1,
    pageSize: number = 20,
    status?: string,
    type?: string
  ): Observable<ScheduledMessagesResponse> {
    let params: any = { page, pageSize };
    if (status) params.status = status;
    if (type) params.type = type;

    return this.http.get<ScheduledMessagesResponse>(`${this.apiUrl}/scheduled-messages`, { params });
  }

  /**
   * Get message type display name
   */
  getMessageTypeDisplayName(type: string): string {
    const displayNames: Record<string, string> = {
      'CheckinDay': 'Check-in Day',
      'MidStay': 'Mid-Stay',
      'PreCheckout': 'Pre-Checkout',
      'PostStay': 'Post-Stay',
      'PreArrival': 'Pre-Arrival',
      'WelcomeSettled': 'Welcome Settled'
    };
    return displayNames[type] || type;
  }

  /**
   * Get message status badge class
   */
  getStatusBadgeClass(status: string): string {
    const statusClasses: Record<string, string> = {
      'Pending': 'bg-warning',
      'Sent': 'bg-success',
      'Failed': 'bg-danger',
      'Cancelled': 'bg-secondary'
    };
    return statusClasses[status] || 'bg-secondary';
  }
}
