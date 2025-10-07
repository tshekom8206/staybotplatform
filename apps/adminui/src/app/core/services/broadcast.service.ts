import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BroadcastMessage {
  id: number;
  messageType: string;
  content: string;
  estimatedRestorationTime?: string;
  status: string;
  totalRecipients: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  createdAt: string;
  completedAt?: string;
  createdBy: string;
}

export interface BroadcastHistoryItem {
  id: number;
  messageType: string;
  status: string;
  totalRecipients: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  createdAt: string;
  completedAt?: string;
  createdBy: string;
}

export interface RecipientGroup {
  id: string;
  type: string;
  name: string;
  description: string;
  count: number;
}

export interface EmergencyBroadcastRequest {
  messageType: string;
  customMessage?: string;
  estimatedRestorationTime?: string;
  broadcastScope: 'ActiveOnly' | 'RecentGuests' | 'AllGuests';
}

export interface GeneralBroadcastRequest {
  title: string;
  content: string;
  recipients: string[];
  priority: 'low' | 'medium' | 'high' | 'urgent';
  scheduledAt?: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

export interface BroadcastTemplate {
  id?: number;
  tenantId?: number;
  name: string;
  category: string;
  subject: string;
  content: string;
  isActive: boolean;
  isDefault: boolean;
  usageCount?: number;
  createdAt?: string;
  updatedAt?: string;
  createdBy?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BroadcastService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/tenant/broadcast`;

  sendEmergencyBroadcast(request: EmergencyBroadcastRequest): Observable<ApiResponse<{ message: string; broadcastId: number }>> {
    return this.http.post<ApiResponse<{ message: string; broadcastId: number }>>(this.apiUrl, request);
  }

  sendGeneralBroadcast(request: GeneralBroadcastRequest): Observable<ApiResponse<{ message: string; broadcastId: number }>> {
    return this.http.post<ApiResponse<{ message: string; broadcastId: number }>>(`${this.apiUrl}/general`, request);
  }

  getBroadcastStatus(broadcastId: number): Observable<ApiResponse<BroadcastMessage>> {
    return this.http.get<ApiResponse<BroadcastMessage>>(`${this.apiUrl}/${broadcastId}`);
  }

  getBroadcastHistory(limit: number = 10): Observable<ApiResponse<BroadcastHistoryItem[]>> {
    return this.http.get<ApiResponse<BroadcastHistoryItem[]>>(`${this.apiUrl}/history?limit=${limit}`);
  }

  getRecipientGroups(): Observable<ApiResponse<RecipientGroup[]>> {
    return this.http.get<ApiResponse<RecipientGroup[]>>(`${this.apiUrl}/recipients`);
  }

  testEmergencyBroadcast(request: EmergencyBroadcastRequest): Observable<ApiResponse<{ message: string; broadcastId: number }>> {
    return this.http.post<ApiResponse<{ message: string; broadcastId: number }>>(`${this.apiUrl}/test`, request);
  }

  // Template management methods
  getTemplates(): Observable<ApiResponse<BroadcastTemplate[]>> {
    return this.http.get<ApiResponse<BroadcastTemplate[]>>(`${this.apiUrl}/templates`);
  }

  getTemplate(id: number): Observable<ApiResponse<BroadcastTemplate>> {
    return this.http.get<ApiResponse<BroadcastTemplate>>(`${this.apiUrl}/templates/${id}`);
  }

  createTemplate(template: Omit<BroadcastTemplate, 'id' | 'tenantId' | 'usageCount' | 'createdAt' | 'updatedAt' | 'createdBy'>): Observable<ApiResponse<BroadcastTemplate>> {
    return this.http.post<ApiResponse<BroadcastTemplate>>(`${this.apiUrl}/templates`, template);
  }

  updateTemplate(id: number, template: Omit<BroadcastTemplate, 'id' | 'tenantId' | 'usageCount' | 'createdAt' | 'updatedAt' | 'createdBy'>): Observable<ApiResponse<BroadcastTemplate>> {
    return this.http.put<ApiResponse<BroadcastTemplate>>(`${this.apiUrl}/templates/${id}`, template);
  }

  deleteTemplate(id: number): Observable<ApiResponse<{ message: string }>> {
    return this.http.delete<ApiResponse<{ message: string }>>(`${this.apiUrl}/templates/${id}`);
  }

  setDefaultTemplate(id: number): Observable<ApiResponse<{ message: string }>> {
    return this.http.post<ApiResponse<{ message: string }>>(`${this.apiUrl}/templates/${id}/set-default`, {});
  }
}