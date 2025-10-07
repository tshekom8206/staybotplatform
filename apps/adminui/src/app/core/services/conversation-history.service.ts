import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface ConversationHistory {
  id: number;
  guestPhone: string;
  guestName?: string;
  roomNumber?: string;
  startedAt: Date;
  endedAt?: Date;
  duration: string;
  messageCount: number;
  status: 'Completed' | 'Transferred' | 'Abandoned';
  lastAgent?: string;
  satisfaction?: number;
  transferredToHuman: boolean;
  transferReason?: string;
  tenantId: number;
}

export interface ConversationHistoryFilters {
  status?: string;
  transferFilter?: string;
  period?: string;
  dateFrom?: Date;
  dateTo?: Date;
  agentId?: number;
  satisfaction?: number;
}

export interface ConversationHistoryStatistics {
  totalConversations: number;
  completedConversations: number;
  transferredConversations: number;
  avgSatisfaction: number;
  avgDurationMinutes: number;
  totalMessageCount: number;
}

export interface MessageExportData {
  id: number;
  conversationId: number;
  messageText: string;
  senderType: string;
  senderName?: string;
  timestamp: Date;
  isSystemMessage: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ConversationHistoryService {
  private apiService = inject(ApiService);
  private http = inject(HttpClient);

  private readonly baseUrl = 'conversations';

  /**
   * Get conversation history from database with optional filters
   */
  getConversationHistory(filters?: ConversationHistoryFilters, page?: number, pageSize?: number): Observable<ConversationHistory[]> {
    let params = new HttpParams();

    if (filters) {
      Object.keys(filters).forEach(key => {
        const value = (filters as any)[key];
        if (value !== null && value !== undefined && value !== '') {
          if (value instanceof Date) {
            params = params.set(key, value.toISOString());
          } else {
            params = params.set(key, value.toString());
          }
        }
      });
    }

    if (page !== undefined) {
      params = params.set('page', page.toString());
    }

    if (pageSize !== undefined) {
      params = params.set('pageSize', pageSize.toString());
    }

    return this.apiService.get<any>(`${this.baseUrl}/history`, params)
      .pipe(
        map(response => {
          // Handle both array response (old format) and object response (new format)
          const conversations = Array.isArray(response) ? response : (response.data || []);
          return conversations.map((conv: any) => this.mapConversationHistory(conv));
        }),
        catchError(error => {
          console.error('Failed to load conversation history from database:', error);
          return throwError(() => new Error('Failed to load conversation history. Please check your connection and try again.'));
        })
      );
  }

  /**
   * Get conversation history statistics from database
   */
  getHistoryStatistics(filters?: ConversationHistoryFilters): Observable<ConversationHistoryStatistics> {
    let params = new HttpParams();

    if (filters) {
      Object.keys(filters).forEach(key => {
        const value = (filters as any)[key];
        if (value !== null && value !== undefined && value !== '') {
          if (value instanceof Date) {
            params = params.set(key, value.toISOString());
          } else {
            params = params.set(key, value.toString());
          }
        }
      });
    }

    return this.apiService.get<any>(`${this.baseUrl}/history/statistics`, params)
      .pipe(
        map(stats => ({
          totalConversations: stats.totalConversations,
          completedConversations: stats.completedConversations,
          transferredConversations: stats.transferredConversations,
          avgSatisfaction: stats.avgSatisfaction,
          avgDurationMinutes: stats.avgDurationMinutes,
          totalMessageCount: stats.totalMessageCount
        })),
        catchError(error => {
          console.error('Failed to load history statistics from database:', error);
          return throwError(() => new Error('Failed to load history statistics. Please try again.'));
        })
      );
  }

  /**
   * Get detailed conversation history with messages from database
   */
  getConversationHistoryDetails(conversationId: number): Observable<ConversationHistory & { messages: MessageExportData[] }> {
    return this.apiService.get<any>(`${this.baseUrl}/${conversationId}/history-details`)
      .pipe(
        map(data => ({
          ...this.mapConversationHistory(data.conversation),
          messages: data.messages?.map((msg: any) => ({
            id: msg.id,
            conversationId: msg.conversationId,
            messageText: msg.messageText,
            senderType: msg.senderType,
            senderName: msg.senderName,
            timestamp: new Date(msg.timestamp),
            isSystemMessage: msg.isSystemMessage
          })) || []
        })),
        catchError(error => {
          console.error('Failed to load conversation history details from database:', error);
          return throwError(() => new Error('Failed to load conversation details. Please try again.'));
        })
      );
  }

  /**
   * Export conversation history to various formats
   */
  exportConversationHistory(format: 'csv' | 'excel' | 'pdf', filters?: ConversationHistoryFilters): Observable<Blob> {
    let params = new HttpParams().set('format', format);

    if (filters) {
      Object.keys(filters).forEach(key => {
        const value = (filters as any)[key];
        if (value !== null && value !== undefined && value !== '') {
          if (value instanceof Date) {
            params = params.set(key, value.toISOString());
          } else {
            params = params.set(key, value.toString());
          }
        }
      });
    }

    return this.http.get(`${this.apiService.getBaseUrl()}/${this.baseUrl}/history/export`, {
      params,
      responseType: 'blob',
      headers: this.apiService.getHeaders()
    }).pipe(
      catchError(error => {
        console.error('Failed to export conversation history from database:', error);
        return throwError(() => new Error('Failed to export conversation history. Please try again.'));
      })
    );
  }

  /**
   * Export single conversation with messages
   */
  exportSingleConversation(conversationId: number, format: 'csv' | 'excel' | 'pdf'): Observable<Blob> {
    const params = new HttpParams().set('format', format);

    return this.http.get(`${this.apiService.getBaseUrl()}/${this.baseUrl}/${conversationId}/export`, {
      params,
      responseType: 'blob',
      headers: this.apiService.getHeaders()
    }).pipe(
      catchError(error => {
        console.error('Failed to export single conversation from database:', error);
        return throwError(() => new Error('Failed to export conversation. Please try again.'));
      })
    );
  }

  /**
   * Search conversation history in database
   */
  searchConversationHistory(query: string, filters?: ConversationHistoryFilters): Observable<ConversationHistory[]> {
    let params = new HttpParams().set('q', query);

    if (filters) {
      Object.keys(filters).forEach(key => {
        const value = (filters as any)[key];
        if (value !== null && value !== undefined && value !== '') {
          if (value instanceof Date) {
            params = params.set(key, value.toISOString());
          } else {
            params = params.set(key, value.toString());
          }
        }
      });
    }

    return this.apiService.get<any[]>(`${this.baseUrl}/history/search`, params)
      .pipe(
        map(conversations => conversations.map(conv => this.mapConversationHistory(conv))),
        catchError(error => {
          console.error('Failed to search conversation history in database:', error);
          return throwError(() => new Error('History search failed. Please try again.'));
        })
      );
  }

  /**
   * Get transfer details for a conversation from database
   */
  getConversationTransferDetails(conversationId: number): Observable<any> {
    return this.apiService.get<any>(`${this.baseUrl}/${conversationId}/transfer-details`)
      .pipe(
        map(transfer => ({
          id: transfer.id,
          conversationId: transfer.conversationId,
          transferReason: transfer.transferReason,
          priority: transfer.priority,
          detectionMethod: transfer.detectionMethod,
          triggerPhrase: transfer.triggerPhrase,
          requestedAt: new Date(transfer.requestedAt),
          acceptedAt: transfer.acceptedAt ? new Date(transfer.acceptedAt) : undefined,
          completedAt: transfer.completedAt ? new Date(transfer.completedAt) : undefined,
          assignedAgent: transfer.assignedAgent,
          handoffContext: transfer.handoffContext
        })),
        catchError(error => {
          console.error('Failed to load transfer details from database:', error);
          return throwError(() => new Error('Failed to load transfer details. Please try again.'));
        })
      );
  }

  /**
   * Map conversation history data from API response
   */
  private mapConversationHistory(conv: any): ConversationHistory {
    return {
      id: conv.id,
      guestPhone: conv.guestPhone,
      guestName: conv.guestName,
      roomNumber: conv.roomNumber,
      startedAt: new Date(conv.startedAt),
      endedAt: conv.endedAt ? new Date(conv.endedAt) : undefined,
      duration: conv.duration,
      messageCount: conv.messageCount,
      status: conv.status,
      lastAgent: conv.lastAgent,
      satisfaction: conv.satisfaction,
      transferredToHuman: conv.transferredToHuman,
      transferReason: conv.transferReason,
      tenantId: conv.tenantId
    };
  }

  /**
   * Download blob as file
   */
  downloadFile(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}