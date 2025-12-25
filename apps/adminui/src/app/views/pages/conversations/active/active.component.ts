import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil, interval, forkJoin } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { ConversationService, ActiveConversation, ConversationStatistics } from '../../../../core/services/conversation.service';
import { TransferService } from '../../../../core/services/transfer.service';
import { AgentService } from '../../../../core/services/agent.service';

@Component({
  selector: 'app-active-conversations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  template: `
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h4 class="page-title">Active Conversations</h4>
      <div class="d-flex gap-2">
        <button class="btn btn-outline-primary btn-sm" (click)="loadConversations()">
          <i feather="refresh-cw" class="me-1"></i>
          Refresh
        </button>
      </div>
    </div>

    <ngb-alert *ngIf="error" type="danger" (closed)="dismissError()">
      {{ error }}
    </ngb-alert>

    <ngb-alert *ngIf="successMessage" type="success" (closed)="dismissSuccess()">
      {{ successMessage }}
    </ngb-alert>

    <!-- Search and Filters -->
    <div class="row mb-4">
      <div class="col-md-6">
        <div class="input-group">
          <span class="input-group-text">
            <i feather="search"></i>
          </span>
          <input
            type="text"
            class="form-control"
            placeholder="Search conversations..."
            [(ngModel)]="searchTerm"
            (input)="applyFilters()">
        </div>
      </div>
      <div class="col-md-3">
        <select class="form-select" [(ngModel)]="selectedStatus" (change)="applyFilters()">
          <option value="">All Status</option>
          <option value="Active">Active</option>
          <option value="Transfer Requested">Transfer Requested</option>
          <option value="Assigned">Assigned</option>
        </select>
      </div>
      <div class="col-md-3">
        <select class="form-select" [(ngModel)]="selectedPriority" (change)="applyFilters()">
          <option value="">All Priorities</option>
          <option value="Emergency">Emergency</option>
          <option value="High">High</option>
          <option value="Normal">Normal</option>
        </select>
      </div>
    </div>

    <!-- Statistics Cards -->
    <div class="row mb-4">
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-primary">{{ totalConversations }}</h5>
            <p class="card-text">Total Active</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-warning">{{ transferRequests }}</h5>
            <p class="card-text">Transfer Requests</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-success">{{ assignedConversations }}</h5>
            <p class="card-text">Assigned</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-danger">{{ emergencyConversations }}</h5>
            <p class="card-text">Emergency</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Conversations Table -->
    <div class="card">
      <div class="card-body">
        <div class="table-responsive">
          <table class="table table-hover">
            <thead>
              <tr>
                <th>Guest</th>
                <th>Room</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Last Message</th>
                <th>Agent</th>
                <th>Messages</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let conversation of filteredConversations; trackBy: conversationTrackBy">
                <td>
                  <div>
                    <strong>{{ conversation.guestName || 'Unknown Guest' }}</strong>
                    <br>
                    <small class="text-muted">{{ conversation.guestPhone }}</small>
                  </div>
                </td>
                <td>
                  <span class="badge bg-secondary" *ngIf="conversation.roomNumber">
                    Room {{ conversation.roomNumber }}
                  </span>
                  <span class="text-muted" *ngIf="!conversation.roomNumber">-</span>
                </td>
                <td>
                  <span class="badge" [ngClass]="getStatusBadgeClass(conversation.status)">
                    {{ conversation.status }}
                  </span>
                  <i feather="alert-triangle"
                     class="text-warning ms-1"
                     *ngIf="conversation.isTransferRequested"
                     ngbTooltip="Transfer requested"></i>
                </td>
                <td>
                  <span class="badge" [ngClass]="getPriorityBadgeClass(conversation.priority)">
                    {{ conversation.priority }}
                  </span>
                </td>
                <td>
                  <div class="text-truncate" style="max-width: 200px;">
                    {{ conversation.lastMessage }}
                  </div>
                  <small class="text-muted">
                    {{ formatTime(conversation.lastMessageTime) }}
                  </small>
                </td>
                <td>
                  <span *ngIf="conversation.assignedAgent" class="badge bg-info">
                    {{ conversation.assignedAgent }}
                  </span>
                  <span *ngIf="!conversation.assignedAgent" class="text-muted">Unassigned</span>
                </td>
                <td>
                  <span class="badge bg-light text-dark">
                    {{ conversation.messageCount }}
                  </span>
                </td>
                <td>
                  <div class="btn-group btn-group-sm" ngbDropdown>
                    <button class="btn btn-outline-primary btn-sm" ngbDropdownToggle>
                      Actions
                    </button>
                    <div class="dropdown-menu" ngbDropdownMenu>
                      <a class="dropdown-item" href="javascript:void(0)" (click)="viewConversation(conversation.id); $event.stopPropagation()">
                        <i feather="eye" class="me-2"></i>View Details
                      </a>
                      <a class="dropdown-item" href="javascript:void(0)" (click)="assignToAgent(conversation.id); $event.stopPropagation()"
                         *ngIf="!conversation.assignedAgent">
                        <i feather="user-plus" class="me-2"></i>Assign Agent
                      </a>
                      <a class="dropdown-item" href="javascript:void(0)" (click)="releaseConversation(conversation.id); $event.stopPropagation()"
                         *ngIf="conversation.assignedAgent">
                        <i feather="user-minus" class="me-2"></i>Release Agent
                      </a>
                      <div class="dropdown-divider" *ngIf="conversation.isTransferRequested"></div>
                      <a class="dropdown-item text-warning" href="javascript:void(0)" (click)="handleTransfer(conversation.id); $event.stopPropagation()"
                         *ngIf="conversation.isTransferRequested">
                        <i feather="arrow-right" class="me-2"></i>Handle Transfer
                      </a>
                    </div>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredConversations.length === 0">
                <td colspan="8" class="text-center text-muted py-4">
                  <i feather="message-circle" class="me-2"></i>
                  No active conversations found
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Conversation Detail Modal -->
    <ng-template #conversationDetailModal let-modal>
      <div class="modal-header">
        <h5 class="modal-title">Conversation Details</h5>
        <button type="button" class="btn-close" aria-label="Close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body">
        <div *ngIf="selectedConversationDetails">
          <!-- Guest Information Card -->
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="mb-0"><i feather="user" class="me-2"></i>Guest Information</h6>
            </div>
            <div class="card-body">
              <div class="row">
                <div class="col-md-6">
                  <p class="mb-2"><strong>Name:</strong> {{ selectedConversationDetails.guestName || 'Unknown Guest' }}</p>
                  <p class="mb-2"><strong>Phone:</strong> {{ selectedConversationDetails.guestPhone }}</p>
                </div>
                <div class="col-md-6">
                  <p class="mb-2"><strong>Room:</strong>
                    <span *ngIf="selectedConversationDetails.roomNumber" class="badge bg-secondary">
                      Room {{ selectedConversationDetails.roomNumber }}
                    </span>
                    <span *ngIf="!selectedConversationDetails.roomNumber">Not Assigned</span>
                  </p>
                  <p class="mb-2"><strong>Priority:</strong>
                    <span class="badge" [ngClass]="getPriorityBadgeClass(selectedConversationDetails.priority)">
                      {{ selectedConversationDetails.priority }}
                    </span>
                  </p>
                </div>
              </div>
            </div>
          </div>

          <!-- Conversation Status Card -->
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="mb-0"><i feather="info" class="me-2"></i>Status Information</h6>
            </div>
            <div class="card-body">
              <div class="row">
                <div class="col-md-6">
                  <p class="mb-2"><strong>Status:</strong>
                    <span class="badge" [ngClass]="getStatusBadgeClass(selectedConversationDetails.status)">
                      {{ selectedConversationDetails.status }}
                    </span>
                  </p>
                  <p class="mb-2"><strong>Assigned Agent:</strong>
                    <span *ngIf="selectedConversationDetails.assignedAgent" class="badge bg-info">
                      {{ selectedConversationDetails.assignedAgent }}
                    </span>
                    <span *ngIf="!selectedConversationDetails.assignedAgent">Unassigned</span>
                  </p>
                </div>
                <div class="col-md-6">
                  <p class="mb-2"><strong>Message Count:</strong> {{ selectedConversationDetails.messageCount }}</p>
                  <p class="mb-2"><strong>Transfer Requested:</strong>
                    <span class="badge" [ngClass]="selectedConversationDetails.isTransferRequested ? 'bg-warning' : 'bg-secondary'">
                      {{ selectedConversationDetails.isTransferRequested ? 'Yes' : 'No' }}
                    </span>
                  </p>
                </div>
              </div>
            </div>
          </div>

          <!-- Messages Card -->
          <div class="card mb-3">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i feather="message-square" class="me-2"></i>Message History</h6>
              <span class="badge bg-primary">{{ conversationMessages.length }} messages</span>
            </div>
            <div class="card-body" style="max-height: 400px; overflow-y: auto;">
              <div *ngIf="conversationMessages.length === 0" class="text-center text-muted py-4">
                <i feather="message-circle" class="me-2"></i>
                No messages found
              </div>
              <div *ngFor="let message of conversationMessages" class="message-item mb-3 pb-3 border-bottom">
                <div class="d-flex justify-content-between align-items-start mb-2">
                  <div class="d-flex align-items-center">
                    <span class="badge me-2" [ngClass]="message.senderType === 'Guest' ? 'bg-primary' : 'bg-success'">
                      <i feather="arrow-down" *ngIf="message.senderType === 'Guest'"></i>
                      <i feather="arrow-up" *ngIf="message.senderType !== 'Guest'"></i>
                      {{ message.senderType }}
                    </span>
                    <strong>{{ message.senderName || message.senderType }}</strong>
                  </div>
                  <small class="text-muted">{{ formatDateTime(message.timestamp) }}</small>
                </div>
                <div class="message-body ps-3">
                  {{ message.messageText }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <div *ngIf="!selectedConversationDetails" class="text-center py-4">
          <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Close</button>
        <button type="button" class="btn btn-primary"
                *ngIf="selectedConversationDetails && !selectedConversationDetails.assignedAgent"
                (click)="assignToAgent(selectedConversationDetails.id); modal.dismiss()">
          <i feather="user-plus" class="me-2"></i>Assign Agent
        </button>
        <button type="button" class="btn btn-warning"
                *ngIf="selectedConversationDetails && selectedConversationDetails.assignedAgent"
                (click)="releaseConversation(selectedConversationDetails.id); modal.dismiss()">
          <i feather="user-minus" class="me-2"></i>Release Agent
        </button>
      </div>
    </ng-template>

    <!-- Agent Selection Modal -->
    <ng-template #agentSelectionModal let-modal>
      <div class="modal-header">
        <h5 class="modal-title">Select Agent</h5>
        <button type="button" class="btn-close" aria-label="Close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body">
        <p class="text-muted mb-3">Select an agent to assign this conversation to:</p>

        <div *ngIf="availableAgents.length === 0" class="text-center text-muted py-4">
          <i feather="users" class="me-2"></i>
          No agents available
        </div>

        <div class="list-group">
          <button
            type="button"
            *ngFor="let agent of availableAgents"
            class="list-group-item list-group-item-action"
            [class.active]="selectedAgent?.id === agent.id"
            (click)="selectedAgent = agent">
            <div class="d-flex justify-content-between align-items-start">
              <div class="flex-grow-1">
                <div class="d-flex align-items-center mb-2">
                  <strong class="me-2">{{ agent.name }}</strong>
                  <span class="badge me-2" [ngClass]="getAgentStateBadgeClass(agent.state)">
                    {{ agent.state }}
                  </span>
                  <span
                    *ngIf="agent.isAvailable"
                    class="badge bg-success-subtle text-success">
                    Available
                  </span>
                </div>
                <div class="text-muted small mb-1">
                  <i feather="mail" class="feather-sm me-1"></i>{{ agent.email }}
                </div>
                <div class="text-muted small mb-1">
                  <i feather="briefcase" class="feather-sm me-1"></i>{{ agent.department }}
                </div>
                <div class="d-flex align-items-center mt-2">
                  <span class="badge me-2" [ngClass]="getWorkloadBadgeClass(agent.activeConversations, agent.maxConcurrentChats)">
                    {{ agent.activeConversations }}/{{ agent.maxConcurrentChats }} conversations
                  </span>
                  <span *ngIf="agent.statusMessage" class="text-muted small">
                    {{ agent.statusMessage }}
                  </span>
                </div>
              </div>
              <div class="ms-3">
                <i *ngIf="selectedAgent?.id === agent.id" feather="check-circle" class="text-success"></i>
              </div>
            </div>
          </button>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Cancel</button>
        <button
          type="button"
          class="btn btn-primary"
          [disabled]="!selectedAgent"
          (click)="confirmAgentAssignment()">
          <i feather="user-check" class="me-2"></i>Assign Agent
        </button>
      </div>
    </ng-template>
  `,
  styleUrls: ['./active.component.scss']
})
export class ActiveConversationsComponent implements OnInit, OnDestroy {
  @ViewChild('conversationDetailModal') conversationDetailModal!: TemplateRef<any>;
  @ViewChild('agentSelectionModal') agentSelectionModal!: TemplateRef<any>;

  private destroy$ = new Subject<void>();
  private conversationService = inject(ConversationService);
  private transferService = inject(TransferService);
  private agentService = inject(AgentService);
  private modalService = inject(NgbModal);

  // Data properties
  conversations: ActiveConversation[] = [];
  filteredConversations: ActiveConversation[] = [];
  // Selected conversation for modal display
  selectedConversationDetails: any = null;
  conversationMessages: any[] = [];

  // Agent selection
  availableAgents: any[] = [];
  selectedAgent: any = null;
  selectedConversationId: number = 0;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedStatus = '';
  selectedPriority = '';

  // Statistics
  totalConversations = 0;
  transferRequests = 0;
  assignedConversations = 0;
  emergencyConversations = 0;

  // Real-time update
  refreshInterval = interval(30000); // Refresh every 30 seconds

  ngOnInit() {
    this.loadConversations();
    this.startRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadConversations() {
    this.loading = true;
    this.error = null;

    // Load conversations from database
    this.conversationService.getActiveConversations()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (conversations) => {
          this.conversations = conversations;
          this.applyFilters();
          this.loadStatistics();
          this.loading = false;
        },
        error: (error) => {
          this.error = error.message;
          this.loading = false;
          console.error('Error loading conversations:', error);
        }
      });
  }

  startRealTimeUpdates() {
    this.refreshInterval.pipe(
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.loadConversations();
    });
  }

  applyFilters() {
    let filtered = [...this.conversations];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(conv =>
        conv.guestName?.toLowerCase().includes(term) ||
        conv.guestPhone.toLowerCase().includes(term) ||
        conv.roomNumber?.toLowerCase().includes(term) ||
        conv.lastMessage.toLowerCase().includes(term)
      );
    }

    // Apply status filter
    if (this.selectedStatus) {
      filtered = filtered.filter(conv => conv.status === this.selectedStatus);
    }

    // Apply priority filter
    if (this.selectedPriority) {
      filtered = filtered.filter(conv => conv.priority === this.selectedPriority);
    }

    this.filteredConversations = filtered;
  }

  loadStatistics() {
    this.conversationService.getConversationStatistics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.totalConversations = stats.totalActive;
          this.transferRequests = stats.transferRequests;
          this.assignedConversations = stats.assigned;
          this.emergencyConversations = stats.emergency;
        },
        error: (error) => {
          console.error('Error loading statistics:', error);
          // Fallback to calculated stats from loaded conversations
          this.updateLocalStatistics();
        }
      });
  }

  updateLocalStatistics() {
    this.totalConversations = this.conversations.length;
    this.transferRequests = this.conversations.filter(c => c.isTransferRequested).length;
    this.assignedConversations = this.conversations.filter(c => c.assignedAgent).length;
    this.emergencyConversations = this.conversations.filter(c => c.priority === 'Emergency').length;
  }

  viewConversation(conversationId: number) {
    // Reset previous data
    this.selectedConversationDetails = null;
    this.conversationMessages = [];

    // Open modal
    this.modalService.open(this.conversationDetailModal, { size: 'xl', scrollable: true });

    // Load conversation details and messages in parallel
    forkJoin({
      details: this.conversationService.getConversationDetails(conversationId),
      messages: this.conversationService.getConversationMessages(conversationId)
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ details, messages }) => {
          this.selectedConversationDetails = details;
          this.conversationMessages = messages;
        },
        error: (error) => {
          this.error = error.message;
          console.error('Error loading conversation details:', error);
        }
      });
  }

  assignToAgent(conversationId: number) {
    this.selectedConversationId = conversationId;
    this.selectedAgent = null;

    // Load available agents
    this.agentService.getAllAgents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (agents) => {
          this.availableAgents = agents;
          // Open agent selection modal
          this.modalService.open(this.agentSelectionModal, { size: 'lg' });
        },
        error: (error) => {
          this.error = 'Failed to load agents: ' + error.message;
        }
      });
  }

  confirmAgentAssignment() {
    if (!this.selectedAgent) {
      this.error = 'Please select an agent';
      return;
    }

    this.conversationService.assignAgentToConversation(this.selectedConversationId, this.selectedAgent.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          const agentName = response.agentName || this.selectedAgent.name;
          this.showSuccessMessage(`Conversation assigned to ${agentName} successfully`);
          this.loadConversations(); // Refresh data
          this.modalService.dismissAll(); // Close modal
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  releaseConversation(conversationId: number) {
    this.conversationService.releaseAgentFromConversation(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage('Agent released successfully');
          this.loadConversations(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  handleTransfer(conversationId: number) {
    // Create manual transfer request
    this.transferService.createManualTransfer(conversationId, 'Manual transfer from active conversations', 'Normal')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (transfer) => {
          this.showSuccessMessage(`Transfer request created successfully (ID: ${transfer.id})`);
          this.loadConversations(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  dismissError() {
    this.error = null;
  }

  dismissSuccess() {
    this.successMessage = null;
  }

  showSuccessMessage(message: string) {
    this.successMessage = message;
    setTimeout(() => {
      this.successMessage = null;
    }, 5000);
  }

  // Utility methods
  getStatusBadgeClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'Active': 'bg-success',
      'Transfer Requested': 'bg-warning',
      'Assigned': 'bg-info',
      'Closed': 'bg-secondary'
    };
    return statusMap[status] || 'bg-secondary';
  }

  getPriorityBadgeClass(priority: string): string {
    const priorityMap: { [key: string]: string } = {
      'Emergency': 'bg-danger',
      'High': 'bg-warning',
      'Normal': 'bg-primary'
    };
    return priorityMap[priority] || 'bg-secondary';
  }

  formatTime(date: Date): string {
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

  conversationTrackBy(index: number, conversation: ActiveConversation): number {
    return conversation.id;
  }

  formatDateTime(date: Date | string): string {
    const d = new Date(date);
    const now = new Date();
    const diff = now.getTime() - d.getTime();
    const minutes = Math.floor(diff / 60000);

    // If today, show time
    if (d.toDateString() === now.toDateString()) {
      if (minutes < 1) return 'Just now';
      if (minutes < 60) return `${minutes}m ago`;

      const hours = Math.floor(minutes / 60);
      return `${hours}h ago`;
    }

    // Otherwise show date and time
    return d.toLocaleString('en-ZA', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Africa/Johannesburg'
    });
  }

  getAgentStateBadgeClass(state: string): string {
    const stateMap: { [key: string]: string } = {
      'Available': 'bg-success',
      'Busy': 'bg-warning',
      'Away': 'bg-secondary',
      'DoNotDisturb': 'bg-danger',
      'Offline': 'bg-dark'
    };
    return stateMap[state] || 'bg-secondary';
  }

  getWorkloadBadgeClass(active: number, max: number): string {
    const percentage = (active / max) * 100;
    if (percentage >= 90) return 'bg-danger';
    if (percentage >= 70) return 'bg-warning';
    if (percentage >= 50) return 'bg-info';
    return 'bg-success';
  }
}
