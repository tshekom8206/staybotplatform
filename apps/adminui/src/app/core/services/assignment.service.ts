import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiService } from './api.service';

export interface ConversationAssignment {
  id: number;
  conversationId: number;
  agentId: number;
  agentName: string;
  agentEmail: string;
  guestPhone: string;
  guestName?: string;
  roomNumber?: string;
  assignedAt: Date;
  releasedAt?: Date;
  status: 'Active' | 'Completed' | 'Transferred' | 'Released';
  priority: 'Emergency' | 'High' | 'Normal';
  responseTime?: string;
  responseTimeSeconds?: number;
  lastActivity: Date;
  messageCount: number;
  transferHistory: AssignmentTransfer[];
  tenantId: number;
}

export interface AssignmentTransfer {
  id: number;
  fromAgentId?: number;
  fromAgentName?: string;
  toAgentId: number;
  toAgentName: string;
  transferredAt: Date;
  reason: string;
  status: 'Completed' | 'Pending' | 'Failed';
}

export interface AssignmentStatistics {
  totalAssignments: number;
  activeAssignments: number;
  completedToday: number;
  avgResponseTimeSeconds: number;
  avgResponseTime: string;
  totalAgentsOnline: number;
  utilizationRate: number;
}

export interface AgentPerformanceMetrics {
  agentId: number;
  agentName: string;
  activeAssignments: number;
  completedToday: number;
  avgResponseTimeSeconds: number;
  avgResponseTime: string;
  totalMessagesHandled: number;
  customerSatisfactionRating?: number;
  isOnline: boolean;
  lastActivity: Date;
}

export interface AssignmentFilters {
  status?: string;
  agentId?: number;
  priority?: string;
  dateFrom?: Date;
  dateTo?: Date;
  roomNumber?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AssignmentService {
  private apiService = inject(ApiService);
  private http = inject(HttpClient);

  private readonly baseUrl = 'assignments';

  /**
   * Get all active assignments from database
   */
  getActiveAssignments(): Observable<ConversationAssignment[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/active`)
      .pipe(
        map(assignments => assignments.map(assignment => this.mapAssignment(assignment))),
        catchError(error => {
          console.error('Failed to load active assignments from database:', error);
          return throwError(() => new Error('Failed to load assignments. Please check your connection and try again.'));
        })
      );
  }

  /**
   * Get assignment history with filters from database
   */
  getAssignmentHistory(filters?: AssignmentFilters): Observable<ConversationAssignment[]> {
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

    return this.apiService.get<any[]>(`${this.baseUrl}/history`, params)
      .pipe(
        map(assignments => assignments.map(assignment => this.mapAssignment(assignment))),
        catchError(error => {
          console.error('Failed to load assignment history from database:', error);
          return throwError(() => new Error('Failed to load assignment history. Please try again.'));
        })
      );
  }

  /**
   * Get assignment statistics from database
   */
  getAssignmentStatistics(): Observable<AssignmentStatistics> {
    return this.apiService.get<any>(`${this.baseUrl}/statistics`)
      .pipe(
        map(stats => ({
          totalAssignments: stats.totalAssignments,
          activeAssignments: stats.activeAssignments,
          completedToday: stats.completedToday,
          avgResponseTimeSeconds: stats.avgResponseTimeSeconds,
          avgResponseTime: this.formatResponseTime(stats.avgResponseTimeSeconds),
          totalAgentsOnline: stats.totalAgentsOnline,
          utilizationRate: stats.utilizationRate
        })),
        catchError(error => {
          console.error('Failed to load assignment statistics from database:', error);
          return throwError(() => new Error('Failed to load assignment statistics. Please try again.'));
        })
      );
  }

  /**
   * Get agent performance metrics from database
   */
  getAgentPerformanceMetrics(): Observable<AgentPerformanceMetrics[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/agent-performance`)
      .pipe(
        map(metrics => metrics.map(metric => ({
          agentId: metric.agentId,
          agentName: metric.agentName,
          activeAssignments: metric.activeAssignments,
          completedToday: metric.completedToday,
          avgResponseTimeSeconds: metric.avgResponseTimeSeconds,
          avgResponseTime: this.formatResponseTime(metric.avgResponseTimeSeconds),
          totalMessagesHandled: metric.totalMessagesHandled,
          customerSatisfactionRating: metric.customerSatisfactionRating,
          isOnline: metric.isOnline,
          lastActivity: new Date(metric.lastActivity)
        }))),
        catchError(error => {
          console.error('Failed to load agent performance metrics from database:', error);
          return throwError(() => new Error('Failed to load agent performance metrics. Please try again.'));
        })
      );
  }

  /**
   * Get detailed assignment information from database
   */
  getAssignmentDetails(assignmentId: number): Observable<ConversationAssignment> {
    return this.apiService.get<any>(`${this.baseUrl}/${assignmentId}/details`)
      .pipe(
        map(assignment => this.mapAssignment(assignment)),
        catchError(error => {
          console.error('Failed to load assignment details from database:', error);
          return throwError(() => new Error('Failed to load assignment details. Please try again.'));
        })
      );
  }

  /**
   * Transfer assignment to another agent via database update
   */
  transferAssignment(assignmentId: number, newAgentId: number, reason: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${assignmentId}/transfer`, {
      newAgentId,
      reason
    }).pipe(
      catchError(error => {
        console.error('Failed to transfer assignment in database:', error);
        return throwError(() => new Error('Failed to transfer assignment. Please try again.'));
      })
    );
  }

  /**
   * Complete assignment via database update
   */
  completeAssignment(assignmentId: number, completionNotes?: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${assignmentId}/complete`, {
      completionNotes
    }).pipe(
      catchError(error => {
        console.error('Failed to complete assignment in database:', error);
        return throwError(() => new Error('Failed to complete assignment. Please try again.'));
      })
    );
  }

  /**
   * Release assignment via database update
   */
  releaseAssignment(assignmentId: number, releaseReason: string): Observable<void> {
    return this.apiService.post<void>(`${this.baseUrl}/${assignmentId}/release`, {
      releaseReason
    }).pipe(
      catchError(error => {
        console.error('Failed to release assignment in database:', error);
        return throwError(() => new Error('Failed to release assignment. Please try again.'));
      })
    );
  }

  /**
   * Bulk transfer assignments to auto-assignment via database update
   */
  bulkAutoAssign(assignmentIds: number[]): Observable<{ successCount: number; failureCount: number }> {
    return this.apiService.post<any>(`${this.baseUrl}/bulk-auto-assign`, {
      assignmentIds
    }).pipe(
      map(response => ({
        successCount: response.successCount,
        failureCount: response.failureCount
      })),
      catchError(error => {
        console.error('Failed to bulk auto-assign in database:', error);
        return throwError(() => new Error('Failed to bulk auto-assign. Please try again.'));
      })
    );
  }

  /**
   * Update assignment priority via database update
   */
  updateAssignmentPriority(assignmentId: number, priority: string): Observable<void> {
    return this.apiService.put<void>(`${this.baseUrl}/${assignmentId}/priority`, {
      priority
    }).pipe(
      catchError(error => {
        console.error('Failed to update assignment priority in database:', error);
        return throwError(() => new Error('Failed to update assignment priority. Please try again.'));
      })
    );
  }

  /**
   * Get assignment transfer history from database
   */
  getAssignmentTransferHistory(assignmentId: number): Observable<AssignmentTransfer[]> {
    return this.apiService.get<any[]>(`${this.baseUrl}/${assignmentId}/transfer-history`)
      .pipe(
        map(transfers => transfers.map(transfer => ({
          id: transfer.id,
          fromAgentId: transfer.fromAgentId,
          fromAgentName: transfer.fromAgentName,
          toAgentId: transfer.toAgentId,
          toAgentName: transfer.toAgentName,
          transferredAt: new Date(transfer.transferredAt),
          reason: transfer.reason,
          status: transfer.status
        }))),
        catchError(error => {
          console.error('Failed to load assignment transfer history from database:', error);
          return throwError(() => new Error('Failed to load transfer history. Please try again.'));
        })
      );
  }

  /**
   * Search assignments in database
   */
  searchAssignments(query: string, filters?: AssignmentFilters): Observable<ConversationAssignment[]> {
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

    return this.apiService.get<any[]>(`${this.baseUrl}/search`, params)
      .pipe(
        map(assignments => assignments.map(assignment => this.mapAssignment(assignment))),
        catchError(error => {
          console.error('Failed to search assignments in database:', error);
          return throwError(() => new Error('Assignment search failed. Please try again.'));
        })
      );
  }

  /**
   * Map assignment data from API response
   */
  private mapAssignment(assignment: any): ConversationAssignment {
    return {
      id: assignment.id,
      conversationId: assignment.conversationId,
      agentId: assignment.agentId,
      agentName: assignment.agentName,
      agentEmail: assignment.agentEmail,
      guestPhone: assignment.guestPhone,
      guestName: assignment.guestName,
      roomNumber: assignment.roomNumber,
      assignedAt: new Date(assignment.assignedAt),
      releasedAt: assignment.releasedAt ? new Date(assignment.releasedAt) : undefined,
      status: assignment.status,
      priority: assignment.priority,
      responseTime: assignment.responseTimeSeconds ?
        this.formatResponseTime(assignment.responseTimeSeconds) : undefined,
      responseTimeSeconds: assignment.responseTimeSeconds,
      lastActivity: new Date(assignment.lastActivity),
      messageCount: assignment.messageCount,
      transferHistory: assignment.transferHistory?.map((t: any) => ({
        id: t.id,
        fromAgentId: t.fromAgentId,
        fromAgentName: t.fromAgentName,
        toAgentId: t.toAgentId,
        toAgentName: t.toAgentName,
        transferredAt: new Date(t.transferredAt),
        reason: t.reason,
        status: t.status
      })) || [],
      tenantId: assignment.tenantId
    };
  }

  /**
   * Format response time from seconds to human readable format
   */
  private formatResponseTime(seconds: number): string {
    if (!seconds || seconds <= 0) return '0s';

    if (seconds < 60) {
      return `${Math.round(seconds)}s`;
    }

    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.round(seconds % 60);

    if (remainingSeconds === 0) {
      return `${minutes}m`;
    }

    return `${minutes}m ${remainingSeconds}s`;
  }
}