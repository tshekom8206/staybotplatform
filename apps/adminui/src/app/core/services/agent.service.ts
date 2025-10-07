import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface AvailableAgent {
  agentId: number;
  name: string;
  email: string;
  department: string;
  skills: string[];
  state: AgentAvailabilityState;
  currentWorkload: number;
  maxConcurrentChats: number;
  availabilityScore: number;
  lastActivity: Date;
  averageResponseTime: string; // TimeSpan as string
  statusMessage?: string;
}

export interface AgentStatus {
  agentId: number;
  tenantId: number;
  state: AgentAvailabilityState;
  statusMessage?: string;
  lastHeartbeat: Date;
  sessionStarted: Date;
  activeConversations: number[];
  department: string;
  skills: string[];
  maxConcurrentChats: number;
}

export interface AgentWorkload {
  agentId: number;
  activeConversations: number;
  pendingTasks: number;
  utilizationPercentage: number;
  averageResponseTime: string;
  activeChats: ConversationSummary[];
}

export interface ConversationSummary {
  conversationId: number;
  phoneNumber: string;
  startedAt: Date;
  lastMessageAt: Date;
  priority: string;
  status: string;
}

export interface AgentStats {
  totalAgents: number;
  availableAgents: number;
  busyAgents: number;
  offlineAgents: number;
  averageWorkload: number;
  totalActiveConversations: number;
  totalPendingTransfers: number;
}

export interface TransferRequest {
  conversationId: number;
  phoneNumber: string;
  preferredDepartment?: string;
  priority: TransferPriority;
  reason: TransferReason;
  requestMessage: string;
  conversationContext: { [key: string]: any };
  requiredSkills: string[];
  isEmergency: boolean;
}

export interface TransferRouting {
  canTransfer: boolean;
  recommendedAgent?: AvailableAgent;
  availableAgents: AvailableAgent[];
  unavailabilityReason?: string;
  estimatedWaitTime?: string;
  alternativeDepartments: string[];
  recommendedStrategy: TransferStrategy;
}

export type AgentAvailabilityState = 'Available' | 'Busy' | 'Away' | 'DoNotDisturb' | 'Offline';
export type TransferPriority = 'Low' | 'Normal' | 'High' | 'Urgent' | 'Emergency';
export type TransferReason = 'UserRequested' | 'SystemEscalation' | 'ComplexityLimit' | 'EmergencyHandoff' | 'SpecialistRequired' | 'QualityAssurance';
export type TransferStrategy = 'ImmediateTransfer' | 'QueuedTransfer' | 'ScheduledCallback' | 'EscalateToManager' | 'CreateTicket';

@Injectable({
  providedIn: 'root'
})
export class AgentService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  // Real-time updates (would be connected to SignalR)
  private agentsSubject = new BehaviorSubject<AvailableAgent[]>([]);
  public agents$ = this.agentsSubject.asObservable();

  getAvailableAgents(department?: string, priority: TransferPriority = 'Normal'): Observable<AvailableAgent[]> {
    let params = new HttpParams();
    if (department) params = params.set('department', department);
    params = params.set('priority', priority);

    return this.http.get<any[]>(`${this.baseUrl}/agents`, { params }).pipe(
      map(agents => agents.map(agent => ({
        agentId: agent.id || agent.agentId,
        name: agent.name,
        email: agent.email,
        department: agent.department,
        skills: agent.skills || [],
        state: agent.state as AgentAvailabilityState,
        currentWorkload: agent.activeConversations || 0,
        maxConcurrentChats: agent.maxConcurrentChats || 5,
        availabilityScore: agent.isAvailable ? 1.0 : 0.0,
        lastActivity: new Date(agent.lastActivity),
        averageResponseTime: '0s',
        statusMessage: agent.statusMessage
      } as AvailableAgent)))
    );
  }

  getAgentStatus(agentId: number): Observable<AgentStatus> {
    return this.http.get<AgentStatus>(`${this.baseUrl}/agent/${agentId}/status`);
  }

  getAgentWorkload(agentId: number): Observable<AgentWorkload> {
    return this.http.get<AgentWorkload>(`${this.baseUrl}/agent/${agentId}/workload`);
  }

  updateAgentStatus(agentId: number, state: AgentAvailabilityState, statusMessage?: string): Observable<any> {
    return this.http.put(`${this.baseUrl}/agent/${agentId}/status`, {
      state,
      statusMessage
    });
  }

  assignConversation(agentId: number, conversationId: number, reason: TransferReason = 'UserRequested'): Observable<any> {
    return this.http.post(`${this.baseUrl}/agent/${agentId}/assign`, {
      conversationId,
      reason
    });
  }

  releaseConversation(conversationId: number, releaseReason: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/agent/release`, {
      conversationId,
      releaseReason
    });
  }

  getDepartmentAgents(department: string): Observable<AgentStatus[]> {
    return this.http.get<AgentStatus[]>(`${this.baseUrl}/agent/department/${department}`);
  }

  getOptimalTransferRouting(request: TransferRequest): Observable<TransferRouting> {
    return this.http.post<TransferRouting>(`${this.baseUrl}/agent/transfer/routing`, request);
  }

  getAgentStats(): Observable<AgentStats> {
    return this.http.get<AgentStats>(`${this.baseUrl}/agents/stats`);
  }

  registerHeartbeat(agentId: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/agent/${agentId}/heartbeat`, {});
  }

  // Utility methods
  getStatusBadgeClass(state: AgentAvailabilityState): string {
    const statusMap: { [key in AgentAvailabilityState]: string } = {
      'Available': 'bg-success',
      'Busy': 'bg-warning',
      'Away': 'bg-secondary',
      'DoNotDisturb': 'bg-danger',
      'Offline': 'bg-dark'
    };
    return statusMap[state] || 'bg-secondary';
  }

  getWorkloadBadgeClass(workload: number, maxConcurrent: number): string {
    const percentage = (workload / maxConcurrent) * 100;
    if (percentage >= 90) return 'bg-danger';
    if (percentage >= 70) return 'bg-warning';
    if (percentage >= 50) return 'bg-info';
    return 'bg-success';
  }

  getPriorityBadgeClass(priority: TransferPriority): string {
    const priorityMap: { [key in TransferPriority]: string } = {
      'Emergency': 'bg-danger',
      'Urgent': 'bg-warning',
      'High': 'bg-info',
      'Normal': 'bg-primary',
      'Low': 'bg-secondary'
    };
    return priorityMap[priority] || 'bg-primary';
  }

  getAvailabilityText(score: number): string {
    if (score >= 0.8) return 'High';
    if (score >= 0.5) return 'Medium';
    if (score >= 0.2) return 'Low';
    return 'None';
  }

  getAvailabilityBadgeClass(score: number): string {
    if (score >= 0.8) return 'bg-success';
    if (score >= 0.5) return 'bg-warning';
    if (score >= 0.2) return 'bg-danger';
    return 'bg-dark';
  }

  formatTimeSpan(timeSpan: string): string {
    // Convert C# TimeSpan string to readable format
    try {
      const parts = timeSpan.split(':');
      if (parts.length >= 2) {
        const hours = parseInt(parts[0]);
        const minutes = parseInt(parts[1]);
        const seconds = parts.length > 2 ? parseInt(parts[2].split('.')[0]) : 0;

        if (hours > 0) return `${hours}h ${minutes}m`;
        if (minutes > 0) return `${minutes}m ${seconds}s`;
        return `${seconds}s`;
      }
    } catch (e) {
      console.warn('Failed to parse TimeSpan:', timeSpan);
    }
    return '0s';
  }

  formatLastActivity(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;

    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;

    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  }

  getDepartments(): string[] {
    return ['General', 'FrontDesk', 'Housekeeping', 'Maintenance', 'Concierge', 'FoodService', 'Security', 'IT'];
  }

  getStatusOptions(): AgentAvailabilityState[] {
    return ['Available', 'Busy', 'Away', 'DoNotDisturb', 'Offline'];
  }

  getPriorityOptions(): TransferPriority[] {
    return ['Low', 'Normal', 'High', 'Urgent', 'Emergency'];
  }

  getTransferReasons(): TransferReason[] {
    return ['UserRequested', 'SystemEscalation', 'ComplexityLimit', 'EmergencyHandoff', 'SpecialistRequired', 'QualityAssurance'];
  }

  // Simple method to get all agents in tenant
  getAllAgents(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/agents`);
  }
}