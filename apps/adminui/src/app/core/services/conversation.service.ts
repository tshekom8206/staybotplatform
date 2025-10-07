import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError, of, from } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { IndexedDBService } from './indexed-db.service';

export interface ActiveConversation {
  id: number;
  guestPhone: string;
  guestName?: string;
  roomNumber?: string;
  status: string;
  lastMessage: string;
  lastMessageTime: Date;
  assignedAgent?: string;
  messageCount: number;
  isTransferRequested?: boolean;
  priority: 'Normal' | 'High' | 'Emergency';
  tenantId: number;
  createdAt: Date;
}

export interface ConversationMessage {
  id: number;
  conversationId: number;
  messageText: string;
  senderType: 'Guest' | 'Bot' | 'Agent';
  senderName?: string;
  timestamp: Date;
  isSystemMessage: boolean;
}

export interface ConversationDetails {
  id: number;
  guestPhone: string;
  guestName?: string;
  roomNumber?: string;
  status: string;
  createdAt: Date;
  endedAt?: Date;
  assignedAgent?: string;
  priority: string;
  messageCount: number;
  lastActivity: Date;
  transferHistory: TransferRecord[];
  assignmentHistory: AssignmentRecord[];
}

export interface TransferRecord {
  id: number;
  requestedAt: Date;
  reason: string;
  status: string;
  assignedAgent?: string;
  completedAt?: Date;
}

export interface AssignmentRecord {
  id: number;
  agentName: string;
  assignedAt: Date;
  releasedAt?: Date;
  isActive: boolean;
}

export interface ConversationStatistics {
  totalActive: number;
  transferRequests: number;
  assigned: number;
  emergency: number;
  avgResponseTime?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ConversationService {
  private apiService = inject(ApiService);
  private http = inject(HttpClient);
  private indexedDBService = inject(IndexedDBService);

  private readonly baseUrl = 'conversations';

  /**
   * Get all active conversations from database
   */
  getActiveConversations(): Observable<ActiveConversation[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/active`)
      .pipe(
        map(conversations => conversations.map(conv => this.mapConversation(conv))),
        tap(conversations => {
          // Save to IndexedDB for offline access
          this.indexedDBService.saveItems('conversations', conversations).catch(err =>
            console.warn('Failed to cache conversations offline:', err)
          );
        }),
        catchError(error => {
          console.warn('API call failed for conversations, falling back to offline data:', error);
          // Fallback to IndexedDB when offline
          return from(this.indexedDBService.getItems<ActiveConversation>('conversations', 100)).pipe(
            map(conversations => conversations.map(conv => this.mapConversation(conv)))
          );
        })
      );
  }

  /**
   * Get conversation statistics from database
   */
  getConversationStatistics(): Observable<ConversationStatistics> {
    return this.apiService.get<any>(`${this.baseUrl}/statistics`)
      .pipe(
        map(stats => ({
          totalActive: stats.totalActive,
          transferRequests: stats.transferRequests,
          assigned: stats.assigned,
          emergency: stats.emergency,
          avgResponseTime: stats.avgResponseTime
        })),
        catchError(error => {
          console.error('Failed to load conversation statistics from database:', error);
          return throwError(() => new Error('Failed to load statistics. Please try again.'));
        })
      );
  }

  /**
   * Get detailed conversation information from database
   */
  getConversationDetails(conversationId: number): Observable<ConversationDetails> {
    return this.apiService.get<any>(`${this.baseUrl}/${conversationId}/details`)
      .pipe(
        map(conv => ({
          id: conv.id,
          guestPhone: conv.guestPhone,
          guestName: conv.guestName,
          roomNumber: conv.roomNumber,
          status: conv.status,
          createdAt: new Date(conv.createdAt),
          endedAt: conv.endedAt ? new Date(conv.endedAt) : undefined,
          assignedAgent: conv.assignedAgent,
          priority: conv.priority,
          messageCount: conv.messageCount,
          lastActivity: new Date(conv.lastActivity),
          transferHistory: conv.transferHistory?.map((t: any) => ({
            id: t.id,
            requestedAt: new Date(t.requestedAt),
            reason: t.reason,
            status: t.status,
            assignedAgent: t.assignedAgent,
            completedAt: t.completedAt ? new Date(t.completedAt) : undefined
          })) || [],
          assignmentHistory: conv.assignmentHistory?.map((a: any) => ({
            id: a.id,
            agentName: a.agentName,
            assignedAt: new Date(a.assignedAt),
            releasedAt: a.releasedAt ? new Date(a.releasedAt) : undefined,
            isActive: a.isActive
          })) || []
        })),
        catchError(error => {
          console.error('Failed to load conversation details from database:', error);
          return throwError(() => new Error('Failed to load conversation details. Please try again.'));
        })
      );
  }

  /**
   * Get conversation messages from database
   */
  getConversationMessages(conversationId: number): Observable<ConversationMessage[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/${conversationId}/messages`)
      .pipe(
        map(messages => messages.map(msg => this.mapMessage(msg))),
        tap(messages => {
          // Save to IndexedDB for offline access
          this.indexedDBService.saveItems('messages', messages).catch(err =>
            console.warn('Failed to cache messages offline:', err)
          );
        }),
        catchError(error => {
          console.warn(`API call failed for messages (conversation ${conversationId}), falling back to offline data:`, error);
          // Fallback to IndexedDB when offline
          return from(this.indexedDBService.getItemsByIndex<ConversationMessage>('messages', 'conversationId', conversationId)).pipe(
            map(messages => messages.map(msg => this.mapMessage(msg)))
          );
        })
      );
  }

  /**
   * Assign agent to conversation via database update
   */
  assignAgentToConversation(conversationId: number, agentId: number): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${conversationId}/assign-agent`, { agentId })
      .pipe(
        catchError(error => {
          console.error('Failed to assign agent in database:', error);
          return throwError(() => new Error('Failed to assign agent. Please try again.'));
        })
      );
  }

  /**
   * Release agent from conversation via database update
   */
  releaseAgentFromConversation(conversationId: number): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${conversationId}/release-agent`, {})
      .pipe(
        catchError(error => {
          console.error('Failed to release agent in database:', error);
          return throwError(() => new Error('Failed to release agent. Please try again.'));
        })
      );
  }

  /**
   * Update conversation priority in database
   */
  updateConversationPriority(conversationId: number, priority: string): Observable<void> {
    return this.apiService.put<void>(`${this.baseUrl}/${conversationId}/priority`, { priority })
      .pipe(
        catchError(error => {
          console.error('Failed to update conversation priority in database:', error);
          return throwError(() => new Error('Failed to update priority. Please try again.'));
        })
      );
  }

  /**
   * Close conversation in database
   */
  closeConversation(conversationId: number, reason?: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${conversationId}/close`, { reason })
      .pipe(
        catchError(error => {
          console.error('Failed to close conversation in database:', error);
          return throwError(() => new Error('Failed to close conversation. Please try again.'));
        })
      );
  }

  /**
   * Search conversations in database
   */
  searchConversations(query: string, filters?: any): Observable<ActiveConversation[]> {
    let params = new HttpParams().set('q', query);

    if (filters) {
      Object.keys(filters).forEach(key => {
        if (filters[key] !== null && filters[key] !== undefined && filters[key] !== '') {
          params = params.set(key, filters[key]);
        }
      });
    }

    return this.apiService.get<any[]>(`${this.baseUrl}/search`, params)
      .pipe(
        map(conversations => conversations.map(conv => ({
          id: conv.id,
          guestPhone: conv.guestPhone,
          guestName: conv.guestName,
          roomNumber: conv.roomNumber,
          status: conv.status,
          lastMessage: conv.lastMessage,
          lastMessageTime: new Date(conv.lastMessageTime),
          assignedAgent: conv.assignedAgent,
          messageCount: conv.messageCount,
          isTransferRequested: conv.isTransferRequested,
          priority: conv.priority,
          tenantId: conv.tenantId,
          createdAt: new Date(conv.createdAt)
        }))),
        catchError(error => {
          console.error('Failed to search conversations in database:', error);
          return throwError(() => new Error('Search failed. Please try again.'));
        })
      );
  }

  /**
   * Private helper methods for mapping data types
   */
  private mapConversation(conv: any): ActiveConversation {
    return {
      id: conv.id,
      guestPhone: conv.guestPhone,
      guestName: conv.guestName,
      roomNumber: conv.roomNumber,
      status: conv.status,
      lastMessage: conv.lastMessage,
      lastMessageTime: new Date(conv.lastMessageTime),
      assignedAgent: conv.assignedAgent,
      messageCount: conv.messageCount,
      isTransferRequested: conv.isTransferRequested,
      priority: conv.priority,
      tenantId: conv.tenantId,
      createdAt: new Date(conv.createdAt)
    };
  }

  private mapMessage(msg: any): ConversationMessage {
    return {
      id: msg.id,
      conversationId: msg.conversationId,
      messageText: msg.messageText,
      senderType: msg.senderType,
      senderName: msg.senderName,
      timestamp: new Date(msg.timestamp),
      isSystemMessage: msg.isSystemMessage
    };
  }
}