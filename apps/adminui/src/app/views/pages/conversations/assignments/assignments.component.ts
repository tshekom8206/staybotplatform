import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil, interval } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModal, NgbModalModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { AssignmentService, ConversationAssignment, AssignmentStatistics } from '../../../../core/services/assignment.service';
import { ConversationService } from '../../../../core/services/conversation.service';
import { AgentService } from '../../../../core/services/agent.service';
import { TransferService } from '../../../../core/services/transfer.service';

@Component({
  selector: 'app-conversation-assignments',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  template: `
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h4 class="page-title">Agent Assignments</h4>
      <div class="d-flex gap-2">
        <button class="btn btn-outline-primary btn-sm" (click)="loadAssignments()">
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

    <!-- Statistics Cards -->
    <div class="row mb-4">
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-primary">{{ totalAssignments }}</h5>
            <p class="card-text">Total Assignments</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-success">{{ activeAssignments }}</h5>
            <p class="card-text">Active</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-info">{{ completedToday }}</h5>
            <p class="card-text">Completed Today</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-warning">{{ avgResponseTime }}</h5>
            <p class="card-text">Avg Response Time</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Search and Filters -->
    <div class="row mb-4">
      <div class="col-md-4">
        <div class="input-group">
          <span class="input-group-text">
            <i feather="search"></i>
          </span>
          <input
            type="text"
            class="form-control"
            placeholder="Search assignments..."
            [(ngModel)]="searchTerm"
            (input)="applyFilters()">
        </div>
      </div>
      <div class="col-md-3">
        <select class="form-select" [(ngModel)]="selectedStatus" (change)="applyFilters()">
          <option value="">All Status</option>
          <option value="Active">Active</option>
          <option value="Completed">Completed</option>
          <option value="Transferred">Transferred</option>
        </select>
      </div>
      <div class="col-md-3">
        <select class="form-select" [(ngModel)]="selectedAgent" (change)="applyFilters()">
          <option value="">All Agents</option>
          <option *ngFor="let agent of getAgents()" [value]="agent">{{ agent }}</option>
        </select>
      </div>
      <div class="col-md-2">
        <button class="btn btn-outline-secondary w-100" (click)="clearFilters()">
          <i feather="x" class="me-1"></i>
          Clear
        </button>
      </div>
    </div>

    <!-- Assignments Table -->
    <div class="card">
      <div class="card-body">
        <div class="table-responsive">
          <table class="table table-hover">
            <thead>
              <tr>
                <th>Agent</th>
                <th>Guest</th>
                <th>Room</th>
                <th>Priority</th>
                <th>Assigned</th>
                <th>Status</th>
                <th>Response Time</th>
                <th>Last Activity</th>
                <th class="text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let assignment of filteredAssignments; trackBy: assignmentTrackBy"
                  [class.table-danger]="assignment.priority === 'Emergency'"
                  [class.table-warning]="assignment.priority === 'High'">
                <td>
                  <div class="d-flex align-items-center">
                    <div class="avatar-sm me-2">
                      <div class="avatar bg-primary text-white">
                        {{ assignment.agentName.charAt(0) }}
                      </div>
                    </div>
                    <strong>{{ assignment.agentName }}</strong>
                  </div>
                </td>
                <td>
                  <div>
                    <strong>{{ assignment.guestName || 'Unknown Guest' }}</strong>
                    <br>
                    <small class="text-muted">{{ assignment.guestPhone }}</small>
                  </div>
                </td>
                <td>
                  <span class="badge bg-secondary" *ngIf="assignment.roomNumber">
                    Room {{ assignment.roomNumber }}
                  </span>
                  <span class="text-muted" *ngIf="!assignment.roomNumber">-</span>
                </td>
                <td>
                  <span class="badge" [ngClass]="getPriorityBadgeClass(assignment.priority)">
                    {{ assignment.priority }}
                  </span>
                </td>
                <td>
                  <div>
                    {{ formatTime(assignment.assignedAt) }}
                    <br>
                    <small class="text-muted">
                      {{ assignment.assignedAt | date:'short' }}
                    </small>
                  </div>
                </td>
                <td>
                  <span class="badge" [ngClass]="getStatusBadgeClass(assignment.status)">
                    {{ assignment.status }}
                  </span>
                </td>
                <td>
                  <span *ngIf="assignment.responseTime" class="badge bg-light text-dark">
                    {{ assignment.responseTime }}
                  </span>
                  <span *ngIf="!assignment.responseTime" class="text-muted">-</span>
                </td>
                <td>
                  <div>
                    {{ formatTime(assignment.lastActivity) }}
                    <br>
                    <small class="text-muted">
                      {{ assignment.lastActivity | date:'short' }}
                    </small>
                  </div>
                </td>
                <td class="text-end">
                  <div ngbDropdown placement="bottom-end" container="body">
                    <button class="btn btn-outline-primary btn-sm" ngbDropdownToggle>
                      Actions
                    </button>
                    <div class="dropdown-menu dropdown-menu-end" ngbDropdownMenu>
                      <button class="dropdown-item" type="button" (click)="viewConversation(assignment.conversationId)">
                        <i feather="eye" class="me-2"></i>View Conversation
                      </button>
                      <button class="dropdown-item" type="button" (click)="transferAssignment(assignment)"
                         *ngIf="assignment.status === 'Active'">
                        <i feather="arrow-right" class="me-2"></i>Transfer
                      </button>
                      <button class="dropdown-item text-success" type="button" (click)="completeAssignment(assignment)"
                         *ngIf="assignment.status === 'Active'">
                        <i feather="check" class="me-2"></i>Mark Complete
                      </button>
                      <div class="dropdown-divider"></div>
                      <button class="dropdown-item text-danger" type="button" (click)="releaseAssignment(assignment)"
                         *ngIf="assignment.status === 'Active'">
                        <i feather="user-minus" class="me-2"></i>Release
                      </button>
                    </div>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredAssignments.length === 0">
                <td colspan="9" class="text-center text-muted py-4">
                  <i feather="user-check" class="me-2"></i>
                  No assignments found
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
        <button type="button" class="btn-close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" *ngIf="selectedConversation">
        <div class="mb-3">
          <h6>Guest Information</h6>
          <p><strong>Name:</strong> {{ selectedConversation.guestName || 'Unknown Guest' }}</p>
          <p><strong>Phone:</strong> {{ selectedConversation.guestPhone }}</p>
          <p *ngIf="selectedConversation.roomNumber"><strong>Room:</strong> {{ selectedConversation.roomNumber }}</p>
        </div>
        <div class="mb-3">
          <h6>Conversation</h6>
          <div class="conversation-messages" style="max-height: 400px; overflow-y: auto;">
            <div *ngFor="let message of selectedConversation.messages"
                 class="message mb-2 p-2 rounded"
                 [ngClass]="message.isFromGuest ? 'bg-light' : 'bg-primary text-white'">
              <small class="d-block">{{ message.timestamp | date:'short' }}</small>
              <div>{{ message.text }}</div>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Close</button>
      </div>
    </ng-template>

    <!-- Transfer Modal -->
    <ng-template #transferModal let-modal>
      <div class="modal-header">
        <h5 class="modal-title">Transfer Assignment</h5>
        <button type="button" class="btn-close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" *ngIf="selectedAssignment">
        <p><strong>Current Assignment:</strong></p>
        <div class="mb-3">
          <p>Agent: {{ selectedAssignment.agentName }}</p>
          <p>Guest: {{ selectedAssignment.guestName || 'Unknown Guest' }}</p>
          <p *ngIf="selectedAssignment.roomNumber">Room: {{ selectedAssignment.roomNumber }}</p>
        </div>

        <div class="mb-3">
          <label class="form-label">Select New Agent:</label>
          <select class="form-select" [(ngModel)]="selectedNewAgentId">
            <option [value]="0">-- Select Agent --</option>
            <option *ngFor="let agent of availableAgents" [value]="agent.agentId">
              {{ agent.name }} ({{ agent.department }})
            </option>
          </select>
        </div>

        <div class="mb-3">
          <label class="form-label">Transfer Reason:</label>
          <textarea class="form-control" rows="3" [(ngModel)]="transferReason"
                    placeholder="Enter reason for transfer..."></textarea>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Cancel</button>
        <button type="button" class="btn btn-primary"
                (click)="confirmTransfer(selectedNewAgentId, transferReason); modal.dismiss()"
                [disabled]="!selectedNewAgentId || selectedNewAgentId === 0">
          Transfer
        </button>
      </div>
    </ng-template>
  `,
  styleUrls: ['./assignments.component.scss']
})
export class ConversationAssignmentsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private assignmentService = inject(AssignmentService);
  private conversationService = inject(ConversationService);
  private modalService = inject(NgbModal);
  private agentService = inject(AgentService);
  private transferService = inject(TransferService);

  // Modal templates
  @ViewChild('conversationDetailModal') conversationDetailModal!: TemplateRef<any>;
  @ViewChild('transferModal') transferModal!: TemplateRef<any>;

  // Data properties
  assignments: ConversationAssignment[] = [];
  filteredAssignments: ConversationAssignment[] = [];
  selectedConversation: any = null;
  selectedAssignment: ConversationAssignment | null = null;
  availableAgents: any[] = [];
  selectedNewAgentId: number = 0;
  transferReason: string = '';

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedStatus = '';
  selectedAgent = '';

  // Statistics
  totalAssignments = 0;
  activeAssignments = 0;
  completedToday = 0;
  avgResponseTime = '0s';

  // Real-time update
  refreshInterval = interval(30000); // Refresh every 30 seconds

  ngOnInit() {
    this.loadAssignments();
    this.startRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAssignments() {
    this.loading = true;
    this.error = null;

    // Load assignments from database
    this.assignmentService.getActiveAssignments()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (assignments) => {
          this.assignments = assignments;
          this.applyFilters();
          this.loadStatistics();
          this.loading = false;
        },
        error: (error) => {
          this.error = error.message;
          this.loading = false;
          console.error('Error loading assignments:', error);
        }
      });
  }

  startRealTimeUpdates() {
    this.refreshInterval.pipe(
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.loadAssignments();
    });
  }

  applyFilters() {
    let filtered = [...this.assignments];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(assignment =>
        assignment.agentName.toLowerCase().includes(term) ||
        assignment.guestName?.toLowerCase().includes(term) ||
        assignment.guestPhone.toLowerCase().includes(term) ||
        assignment.roomNumber?.toLowerCase().includes(term)
      );
    }

    // Apply status filter
    if (this.selectedStatus) {
      filtered = filtered.filter(assignment => assignment.status === this.selectedStatus);
    }

    // Apply agent filter
    if (this.selectedAgent) {
      filtered = filtered.filter(assignment => assignment.agentName === this.selectedAgent);
    }

    // Sort by priority and assignment time
    filtered.sort((a, b) => {
      const priorityOrder = { 'Emergency': 3, 'High': 2, 'Normal': 1 };
      const priorityDiff = priorityOrder[b.priority] - priorityOrder[a.priority];
      if (priorityDiff !== 0) return priorityDiff;
      return new Date(b.assignedAt).getTime() - new Date(a.assignedAt).getTime();
    });

    this.filteredAssignments = filtered;
  }

  loadStatistics() {
    this.assignmentService.getAssignmentStatistics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.totalAssignments = stats.totalAssignments;
          this.activeAssignments = stats.activeAssignments;
          this.completedToday = stats.completedToday;
          this.avgResponseTime = stats.avgResponseTime;
        },
        error: (error) => {
          console.error('Error loading assignment statistics:', error);
          // Fallback to calculated stats from loaded assignments
          this.updateLocalStatistics();
        }
      });
  }

  updateLocalStatistics() {
    this.totalAssignments = this.assignments.length;
    this.activeAssignments = this.assignments.filter(a => a.status === 'Active').length;

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this.completedToday = this.assignments.filter(a =>
      a.status === 'Completed' && new Date(a.assignedAt) >= today
    ).length;

    // Calculate average response time
    const responseTimesWithTime = this.assignments
      .filter(a => a.responseTime)
      .map(a => this.parseResponseTime(a.responseTime!));

    if (responseTimesWithTime.length > 0) {
      const avgSeconds = responseTimesWithTime.reduce((sum, time) => sum + time, 0) / responseTimesWithTime.length;
      this.avgResponseTime = this.formatSeconds(avgSeconds);
    } else {
      this.avgResponseTime = '0s';
    }
  }

  parseResponseTime(timeStr: string): number {
    // Parse "2m 30s" format to seconds
    const parts = timeStr.split(' ');
    let seconds = 0;
    for (const part of parts) {
      if (part.endsWith('m')) {
        seconds += parseInt(part) * 60;
      } else if (part.endsWith('s')) {
        seconds += parseInt(part);
      }
    }
    return seconds;
  }

  formatSeconds(seconds: number): string {
    if (seconds < 60) return `${Math.round(seconds)}s`;
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.round(seconds % 60);
    if (remainingSeconds === 0) return `${minutes}m`;
    return `${minutes}m ${remainingSeconds}s`;
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedStatus = '';
    this.selectedAgent = '';
    this.applyFilters();
  }

  viewConversation(conversationId: number) {
    this.loading = true;
    this.conversationService.getConversationDetails(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (details) => {
          this.loading = false;
          this.selectedConversation = details;
          this.modalService.open(this.conversationDetailModal, { size: 'lg', scrollable: true });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  transferAssignment(assignment: ConversationAssignment) {
    this.selectedAssignment = assignment;
    this.loading = true;

    // Load available agents
    this.agentService.getAvailableAgents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (agents) => {
          this.loading = false;
          this.availableAgents = agents.filter(a => a.agentId !== assignment.agentId);
          this.modalService.open(this.transferModal, { size: 'md' });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  confirmTransfer(newAgentId: number, reason: string) {
    if (!this.selectedAssignment) return;

    this.assignmentService.transferAssignment(this.selectedAssignment.id, newAgentId, reason)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Assignment #${this.selectedAssignment!.id} transferred successfully`);
          this.modalService.dismissAll();
          this.loadAssignments();
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  completeAssignment(assignment: ConversationAssignment) {
    this.assignmentService.completeAssignment(assignment.id, 'Assignment completed successfully')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Assignment #${assignment.id} marked as completed`);
          this.loadAssignments(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  releaseAssignment(assignment: ConversationAssignment) {
    this.assignmentService.releaseAssignment(assignment.id, 'Released by administrator')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Assignment #${assignment.id} released`);
          this.loadAssignments(); // Refresh data
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
  getPriorityBadgeClass(priority: string): string {
    const priorityMap: { [key: string]: string } = {
      'Emergency': 'bg-danger',
      'High': 'bg-warning',
      'Normal': 'bg-primary'
    };
    return priorityMap[priority] || 'bg-secondary';
  }

  getStatusBadgeClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'Active': 'bg-success',
      'Completed': 'bg-info',
      'Transferred': 'bg-warning'
    };
    return statusMap[status] || 'bg-secondary';
  }

  getAgents(): string[] {
    return [...new Set(this.assignments.map(a => a.agentName))].sort();
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

  assignmentTrackBy(index: number, assignment: ConversationAssignment): number {
    return assignment.id;
  }
}