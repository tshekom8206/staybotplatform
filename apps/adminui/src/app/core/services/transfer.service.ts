import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface TransferRequest {
  id: number;
  conversationId: number;
  guestPhone: string;
  guestName?: string;
  roomNumber?: string;
  transferReason: string;
  priority: 'Emergency' | 'High' | 'Normal';
  detectionMethod: string;
  triggerPhrase: string;
  requestedAt: Date;
  status: 'Pending' | 'InProgress' | 'Completed' | 'Cancelled';
  assignedAgent?: string;
  handoffContext?: HandoffContext;
  tenantId: number;
}

export interface HandoffContext {
  conversationSummary: string;
  guestMood: string;
  unresolved_issues: string[];
  conversation_highlights: string[];
  lastMessages: ConversationMessage[];
  handoffSummary: string;
  contextualInformation: string;
  suggestedResponse: string;
}

export interface ConversationMessage {
  id: number;
  messageText: string;
  senderType: 'Guest' | 'Bot' | 'Agent';
  timestamp: Date;
  isSystemMessage: boolean;
}

export interface TransferStatistics {
  pendingTransfers: number;
  emergencyTransfers: number;
  inProgressTransfers: number;
  completedToday: number;
  avgProcessingTime?: string;
}

export interface AgentInfo {
  id: number;
  name: string;
  email: string;
  isAvailable: boolean;
  currentLoad: number;
  maxConcurrentChats: number;
  specializations: string[];
}

@Injectable({
  providedIn: 'root'
})
export class TransferService {
  private apiService = inject(ApiService);
  private http = inject(HttpClient);

  private readonly baseUrl = 'transfers';

  /**
   * Get all transfer requests from database
   */
  getTransferQueue(): Observable<TransferRequest[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/queue`)
      .pipe(
        map(transfers => transfers.map(transfer => ({
          id: transfer.id,
          conversationId: transfer.conversationId,
          guestPhone: transfer.guestPhone,
          guestName: transfer.guestName,
          roomNumber: transfer.roomNumber,
          transferReason: transfer.transferReason,
          priority: transfer.priority,
          detectionMethod: transfer.detectionMethod,
          triggerPhrase: transfer.triggerPhrase,
          requestedAt: new Date(transfer.requestedAt),
          status: transfer.status,
          assignedAgent: transfer.assignedAgent,
          handoffContext: transfer.handoffContext,
          tenantId: transfer.tenantId
        }))),
        catchError(error => {
          console.error('Failed to load transfer queue from database:', error);
          return throwError(() => new Error('Failed to load transfer queue. Please check your connection and try again.'));
        })
      );
  }

  /**
   * Get transfer statistics from database
   */
  getTransferStatistics(): Observable<TransferStatistics> {
    return this.apiService.get<any>(`${this.baseUrl}/statistics`)
      .pipe(
        map(stats => ({
          pendingTransfers: stats.pendingTransfers,
          emergencyTransfers: stats.emergencyTransfers,
          inProgressTransfers: stats.inProgressTransfers,
          completedToday: stats.completedToday,
          avgProcessingTime: stats.avgProcessingTime
        })),
        catchError(error => {
          console.error('Failed to load transfer statistics from database:', error);
          return throwError(() => new Error('Failed to load transfer statistics. Please try again.'));
        })
      );
  }

  /**
   * Get detailed transfer information with handoff context from database
   */
  getTransferDetails(transferId: number): Observable<TransferRequest> {
    return this.apiService.get<any>(`${this.baseUrl}/${transferId}/details`)
      .pipe(
        map(transfer => ({
          id: transfer.id,
          conversationId: transfer.conversationId,
          guestPhone: transfer.guestPhone,
          guestName: transfer.guestName,
          roomNumber: transfer.roomNumber,
          transferReason: transfer.transferReason,
          priority: transfer.priority,
          detectionMethod: transfer.detectionMethod,
          triggerPhrase: transfer.triggerPhrase,
          requestedAt: new Date(transfer.requestedAt),
          status: transfer.status,
          assignedAgent: transfer.assignedAgent,
          handoffContext: transfer.handoffContext ? {
            conversationSummary: transfer.handoffContext.conversationSummary,
            guestMood: transfer.handoffContext.guestMood,
            unresolved_issues: transfer.handoffContext.unresolved_issues,
            conversation_highlights: transfer.handoffContext.conversation_highlights,
            lastMessages: transfer.handoffContext.lastMessages?.map((msg: any) => ({
              id: msg.id,
              messageText: msg.messageText,
              senderType: msg.senderType,
              timestamp: new Date(msg.timestamp),
              isSystemMessage: msg.isSystemMessage
            })) || [],
            handoffSummary: transfer.handoffContext.handoffSummary,
            contextualInformation: transfer.handoffContext.contextualInformation,
            suggestedResponse: transfer.handoffContext.suggestedResponse
          } : undefined,
          tenantId: transfer.tenantId
        })),
        catchError(error => {
          console.error('Failed to load transfer details from database:', error);
          return throwError(() => new Error('Failed to load transfer details. Please try again.'));
        })
      );
  }

  /**
   * Accept a transfer request and assign to agent via database update
   */
  acceptTransfer(transferId: number, agentId?: number): Observable<void> {
    const payload = agentId ? { agentId } : {};
    return this.apiService.post<void>(`${this.baseUrl}/${transferId}/accept`, payload)
      .pipe(
        catchError(error => {
          console.error('Failed to accept transfer in database:', error);
          return throwError(() => new Error('Failed to accept transfer. Please try again.'));
        })
      );
  }

  /**
   * Assign specific agent to transfer via database update
   */
  assignAgentToTransfer(transferId: number, agentId: number): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${transferId}/assign-agent`, { agentId })
      .pipe(
        catchError(error => {
          console.error('Failed to assign agent to transfer in database:', error);
          return throwError(() => new Error('Failed to assign agent. Please try again.'));
        })
      );
  }

  /**
   * Complete a transfer via database update
   */
  completeTransfer(transferId: number, completionNotes?: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${transferId}/complete`, { completionNotes })
      .pipe(
        catchError(error => {
          console.error('Failed to complete transfer in database:', error);
          return throwError(() => new Error('Failed to complete transfer. Please try again.'));
        })
      );
  }

  /**
   * Cancel a transfer via database update
   */
  cancelTransfer(transferId: number, cancellationReason: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${transferId}/cancel`, { cancellationReason })
      .pipe(
        catchError(error => {
          console.error('Failed to cancel transfer in database:', error);
          return throwError(() => new Error('Failed to cancel transfer. Please try again.'));
        })
      );
  }

  /**
   * Process all pending transfers automatically
   */
  processAllPendingTransfers(): Observable<{ processedCount: number }> {
    return this.apiService.post<any>(`${this.baseUrl}/process-all-pending`, {})
      .pipe(
        map(response => ({ processedCount: response.processedCount })),
        catchError(error => {
          console.error('Failed to process pending transfers in database:', error);
          return throwError(() => new Error('Failed to process pending transfers. Please try again.'));
        })
      );
  }

  /**
   * Get available agents for transfer assignment from database
   */
  getAvailableAgents(): Observable<AgentInfo[]> {
    return this.apiService.get<any[]>(`agents`)
      .pipe(
        map(agents => agents.map(agent => ({
          id: agent.id,
          name: agent.name,
          email: agent.email,
          isAvailable: agent.isAvailable,
          currentLoad: agent.currentLoad,
          maxConcurrentChats: agent.maxConcurrentChats,
          specializations: agent.specializations || []
        }))),
        catchError(error => {
          console.error('Failed to load available agents from database:', error);
          return throwError(() => new Error('Failed to load available agents. Please try again.'));
        })
      );
  }

  /**
   * Create manual transfer request via database
   */
  createManualTransfer(conversationId: number, reason: string, priority: string): Observable<TransferRequest> {
    return this.apiService.post<any>(`${this.baseUrl}/create-manual`, {
      conversationId,
      reason,
      priority
    }).pipe(
      map(transfer => ({
        id: transfer.id,
        conversationId: transfer.conversationId,
        guestPhone: transfer.guestPhone,
        guestName: transfer.guestName,
        roomNumber: transfer.roomNumber,
        transferReason: transfer.transferReason,
        priority: transfer.priority,
        detectionMethod: transfer.detectionMethod,
        triggerPhrase: transfer.triggerPhrase,
        requestedAt: new Date(transfer.requestedAt),
        status: transfer.status,
        assignedAgent: transfer.assignedAgent,
        handoffContext: transfer.handoffContext,
        tenantId: transfer.tenantId
      })),
      catchError(error => {
        console.error('Failed to create manual transfer in database:', error);
        return throwError(() => new Error('Failed to create transfer request. Please try again.'));
      })
    );
  }

  /**
   * Search transfers in database
   */
  searchTransfers(query: string, filters?: any): Observable<TransferRequest[]> {
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
        map(transfers => transfers.map(transfer => ({
          id: transfer.id,
          conversationId: transfer.conversationId,
          guestPhone: transfer.guestPhone,
          guestName: transfer.guestName,
          roomNumber: transfer.roomNumber,
          transferReason: transfer.transferReason,
          priority: transfer.priority,
          detectionMethod: transfer.detectionMethod,
          triggerPhrase: transfer.triggerPhrase,
          requestedAt: new Date(transfer.requestedAt),
          status: transfer.status,
          assignedAgent: transfer.assignedAgent,
          handoffContext: transfer.handoffContext,
          tenantId: transfer.tenantId
        }))),
        catchError(error => {
          console.error('Failed to search transfers in database:', error);
          return throwError(() => new Error('Transfer search failed. Please try again.'));
        })
      );
  }
}