import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, interval } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { TransferService, TransferRequest, TransferStatistics, AgentInfo } from '../../../../core/services/transfer.service';

@Component({
  selector: 'app-transfer-queue',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  template: `
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h4 class="page-title">Transfer Queue</h4>
      <div class="d-flex gap-2">
        <button class="btn btn-outline-primary btn-sm" (click)="loadTransfers()">
          <i feather="refresh-cw" class="me-1"></i>
          Refresh
        </button>
        <button class="btn btn-primary btn-sm" (click)="processAllPending()">
          <i feather="play-circle" class="me-1"></i>
          Process All Pending
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
        <div class="card border-warning">
          <div class="card-body text-center">
            <h5 class="card-title text-warning">{{ pendingTransfers }}</h5>
            <p class="card-text">Pending Transfers</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-danger">
          <div class="card-body text-center">
            <h5 class="card-title text-danger">{{ emergencyTransfers }}</h5>
            <p class="card-text">Emergency</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-info">
          <div class="card-body text-center">
            <h5 class="card-title text-info">{{ inProgressTransfers }}</h5>
            <p class="card-text">In Progress</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card border-success">
          <div class="card-body text-center">
            <h5 class="card-title text-success">{{ completedToday }}</h5>
            <p class="card-text">Completed Today</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Filter Tabs -->
    <ul class="nav nav-tabs nav-tabs-line mb-3">
      <li class="nav-item">
        <a class="nav-link"
           [class.active]="activeTab === 'pending'"
           (click)="setActiveTab('pending')">
          Pending <span class="badge bg-warning text-dark ms-1">{{ pendingTransfers }}</span>
        </a>
      </li>
      <li class="nav-item">
        <a class="nav-link"
           [class.active]="activeTab === 'emergency'"
           (click)="setActiveTab('emergency')">
          Emergency <span class="badge bg-danger ms-1">{{ emergencyTransfers }}</span>
        </a>
      </li>
      <li class="nav-item">
        <a class="nav-link"
           [class.active]="activeTab === 'inprogress'"
           (click)="setActiveTab('inprogress')">
          In Progress <span class="badge bg-info text-dark ms-1">{{ inProgressTransfers }}</span>
        </a>
      </li>
      <li class="nav-item">
        <a class="nav-link"
           [class.active]="activeTab === 'all'"
           (click)="setActiveTab('all')">
          All
        </a>
      </li>
    </ul>

    <!-- Search -->
    <div class="row mb-3">
      <div class="col-md-6">
        <div class="input-group">
          <span class="input-group-text">
            <i feather="search"></i>
          </span>
          <input
            type="text"
            class="form-control"
            placeholder="Search transfers..."
            [(ngModel)]="searchTerm"
            (input)="applyFilters()">
        </div>
      </div>
    </div>

    <!-- Transfer Requests Table -->
    <div class="card">
      <div class="card-body">
        <div class="table-responsive">
          <table class="table table-hover">
            <thead>
              <tr>
                <th>Guest</th>
                <th>Room</th>
                <th>Priority</th>
                <th>Reason</th>
                <th>Detection</th>
                <th>Requested</th>
                <th>Status</th>
                <th>Agent</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let transfer of filteredTransfers; trackBy: transferTrackBy"
                  [class.table-danger]="transfer.priority === 'Emergency'"
                  [class.table-warning]="transfer.priority === 'High'">
                <td>
                  <div>
                    <strong>{{ transfer.guestName || 'Unknown Guest' }}</strong>
                    <br>
                    <small class="text-muted">{{ transfer.guestPhone }}</small>
                  </div>
                </td>
                <td>
                  <span class="badge bg-secondary" *ngIf="transfer.roomNumber">
                    Room {{ transfer.roomNumber }}
                  </span>
                  <span class="text-muted" *ngIf="!transfer.roomNumber">-</span>
                </td>
                <td>
                  <span class="badge" [ngClass]="getPriorityBadgeClass(transfer.priority)">
                    <i feather="alert-triangle"
                       class="me-1"
                       *ngIf="transfer.priority === 'Emergency'"></i>
                    {{ transfer.priority }}
                  </span>
                </td>
                <td>
                  <div>
                    <strong>{{ getReasonDisplay(transfer.transferReason) }}</strong>
                    <br>
                    <small class="text-muted"
                           [ngbTooltip]="transfer.triggerPhrase">
                      {{ transfer.detectionMethod | titlecase }}
                    </small>
                  </div>
                </td>
                <td>
                  <span class="badge" [ngClass]="getDetectionBadgeClass(transfer.detectionMethod)">
                    {{ transfer.detectionMethod | titlecase }}
                  </span>
                </td>
                <td>
                  <div>
                    {{ formatTime(transfer.requestedAt) }}
                    <br>
                    <small class="text-muted">
                      {{ transfer.requestedAt | date:'short' }}
                    </small>
                  </div>
                </td>
                <td>
                  <span class="badge" [ngClass]="getStatusBadgeClass(transfer.status)">
                    {{ transfer.status }}
                  </span>
                </td>
                <td>
                  <span *ngIf="transfer.assignedAgent" class="badge bg-info">
                    {{ transfer.assignedAgent }}
                  </span>
                  <span *ngIf="!transfer.assignedAgent" class="text-muted">Unassigned</span>
                </td>
                <td>
                  <div class="btn-group btn-group-sm" ngbDropdown>
                    <button class="btn btn-outline-primary btn-sm" ngbDropdownToggle>
                      Actions
                    </button>
                    <div class="dropdown-menu" ngbDropdownMenu>
                      <button class="dropdown-item" type="button" (click)="viewHandoffContext(transfer)">
                        <i feather="eye" class="me-2"></i>View Context
                      </button>
                      <button class="dropdown-item" type="button" (click)="assignAgent(transfer)"
                         *ngIf="transfer.status === 'Pending'">
                        <i feather="user-plus" class="me-2"></i>Assign Agent
                      </button>
                      <button class="dropdown-item" type="button" (click)="acceptTransfer(transfer)"
                         *ngIf="transfer.status === 'Pending'">
                        <i feather="check-circle" class="me-2"></i>Accept Transfer
                      </button>
                      <button class="dropdown-item text-success" type="button" (click)="completeTransfer(transfer)"
                         *ngIf="transfer.status === 'InProgress'">
                        <i feather="check" class="me-2"></i>Complete
                      </button>
                      <div class="dropdown-divider" *ngIf="transfer.status !== 'Completed' && transfer.status !== 'Cancelled'"></div>
                      <button class="dropdown-item text-danger" type="button" (click)="cancelTransfer(transfer)"
                         *ngIf="transfer.status !== 'Completed' && transfer.status !== 'Cancelled'">
                        <i feather="x-circle" class="me-2"></i>Cancel
                      </button>
                    </div>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredTransfers.length === 0">
                <td colspan="9" class="text-center text-muted py-4">
                  <i feather="arrow-right-circle" class="me-2"></i>
                  No transfer requests found
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Handoff Context Modal -->
    <ng-template #contextModal let-modal>
      <div class="modal-header">
        <h4 class="modal-title">Transfer Context</h4>
        <button type="button" class="btn-close" aria-label="Close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" *ngIf="selectedTransfer">
        <div class="row">
          <div class="col-md-6">
            <h6>Guest Information</h6>
            <ul class="list-unstyled">
              <li><strong>Name:</strong> {{ selectedTransfer.guestName || 'Unknown' }}</li>
              <li><strong>Phone:</strong> {{ selectedTransfer.guestPhone }}</li>
              <li><strong>Room:</strong> {{ selectedTransfer.roomNumber || 'Not specified' }}</li>
            </ul>
          </div>
          <div class="col-md-6">
            <h6>Transfer Details</h6>
            <ul class="list-unstyled">
              <li><strong>Reason:</strong> {{ getReasonDisplay(selectedTransfer.transferReason) }}</li>
              <li><strong>Priority:</strong> {{ selectedTransfer.priority }}</li>
              <li><strong>Detection:</strong> {{ selectedTransfer.detectionMethod }}</li>
            </ul>
          </div>
        </div>

        <div class="mt-3">
          <h6>Trigger Message</h6>
          <div class="alert alert-light">
            "{{ selectedTransfer.triggerPhrase }}"
          </div>
        </div>

        <div class="mt-3" *ngIf="selectedTransfer.handoffContext">
          <h6>Handoff Summary</h6>
          <div class="alert alert-info">
            {{ selectedTransfer.handoffContext.handoffSummary || 'No summary available' }}
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.dismiss()">Close</button>
        <button type="button" class="btn btn-primary" (click)="acceptTransfer(selectedTransfer!); modal.dismiss()"
                *ngIf="selectedTransfer?.status === 'Pending'">
          Accept Transfer
        </button>
      </div>
    </ng-template>
  `,
  styleUrls: ['./transfers.component.scss']
})
export class TransferQueueComponent implements OnInit, OnDestroy {
  @ViewChild('contextModal', { static: true }) contextModalTemplate!: TemplateRef<any>;

  private destroy$ = new Subject<void>();
  private modalService = inject(NgbModal);
  private transferService = inject(TransferService);

  // Data properties
  transfers: TransferRequest[] = [];
  filteredTransfers: TransferRequest[] = [];
  selectedTransfer: TransferRequest | null = null;
  availableAgents: AgentInfo[] = [];

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  activeTab = 'pending';

  // Statistics
  pendingTransfers = 0;
  emergencyTransfers = 0;
  inProgressTransfers = 0;
  completedToday = 0;

  // Real-time update
  refreshInterval = interval(15000); // Refresh every 15 seconds for transfers

  ngOnInit() {
    this.loadTransfers();
    this.startRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadTransfers() {
    this.loading = true;
    this.error = null;

    // Load transfers from database
    this.transferService.getTransferQueue()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (transfers) => {
          this.transfers = transfers;
          this.applyFilters();
          this.loadStatistics();
          this.loadAvailableAgents();
          this.loading = false;
        },
        error: (error) => {
          this.error = error.message;
          this.loading = false;
          console.error('Error loading transfers:', error);
        }
      });
  }

  startRealTimeUpdates() {
    this.refreshInterval.pipe(
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.loadTransfers();
    });
  }

  applyFilters() {
    let filtered = [...this.transfers];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(transfer =>
        transfer.guestName?.toLowerCase().includes(term) ||
        transfer.guestPhone.toLowerCase().includes(term) ||
        transfer.roomNumber?.toLowerCase().includes(term) ||
        transfer.triggerPhrase.toLowerCase().includes(term)
      );
    }

    // Apply tab filter
    switch (this.activeTab) {
      case 'pending':
        filtered = filtered.filter(t => t.status === 'Pending');
        break;
      case 'emergency':
        filtered = filtered.filter(t => t.priority === 'Emergency');
        break;
      case 'inprogress':
        filtered = filtered.filter(t => t.status === 'InProgress');
        break;
    }

    // Sort by priority and time
    filtered.sort((a, b) => {
      const priorityOrder = { 'Emergency': 3, 'High': 2, 'Normal': 1 };
      const priorityDiff = priorityOrder[b.priority] - priorityOrder[a.priority];
      if (priorityDiff !== 0) return priorityDiff;
      return new Date(a.requestedAt).getTime() - new Date(b.requestedAt).getTime();
    });

    this.filteredTransfers = filtered;
  }

  loadStatistics() {
    this.transferService.getTransferStatistics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.pendingTransfers = stats.pendingTransfers;
          this.emergencyTransfers = stats.emergencyTransfers;
          this.inProgressTransfers = stats.inProgressTransfers;
          this.completedToday = stats.completedToday;
        },
        error: (error) => {
          console.error('Error loading transfer statistics:', error);
          // Fallback to calculated stats from loaded transfers
          this.updateLocalStatistics();
        }
      });
  }

  updateLocalStatistics() {
    this.pendingTransfers = this.transfers.filter(t => t.status === 'Pending').length;
    this.emergencyTransfers = this.transfers.filter(t => t.priority === 'Emergency').length;
    this.inProgressTransfers = this.transfers.filter(t => t.status === 'InProgress').length;

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    this.completedToday = this.transfers.filter(t =>
      t.status === 'Completed' && new Date(t.requestedAt) >= today
    ).length;
  }

  loadAvailableAgents() {
    this.transferService.getAvailableAgents()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (agents) => {
          this.availableAgents = agents;
        },
        error: (error) => {
          console.error('Error loading available agents:', error);
        }
      });
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
    this.applyFilters();
  }

  viewHandoffContext(transfer: TransferRequest) {
    this.loading = true;
    this.transferService.getTransferDetails(transfer.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (transferDetails) => {
          this.selectedTransfer = transferDetails;
          this.loading = false;
          this.modalService.open(this.contextModalTemplate, {
            size: 'lg',
            backdrop: 'static'
          });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  assignAgent(transfer: TransferRequest) {
    // TODO: Open agent selection modal, for now use first available agent
    if (this.availableAgents.length === 0) {
      this.error = 'No agents available for assignment';
      return;
    }

    const selectedAgent = this.availableAgents[0]; // This should come from agent selection modal

    this.transferService.assignAgentToTransfer(transfer.id, selectedAgent.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Transfer assigned to ${selectedAgent.name}`);
          this.loadTransfers(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  acceptTransfer(transfer: TransferRequest) {
    this.transferService.acceptTransfer(transfer.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Transfer #${transfer.id} accepted successfully`);
          this.loadTransfers(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  completeTransfer(transfer: TransferRequest) {
    this.transferService.completeTransfer(transfer.id, 'Transfer completed successfully')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Transfer #${transfer.id} completed successfully`);
          this.loadTransfers(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  cancelTransfer(transfer: TransferRequest) {
    this.transferService.cancelTransfer(transfer.id, 'Cancelled by administrator')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage(`Transfer #${transfer.id} cancelled`);
          this.loadTransfers(); // Refresh data
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  processAllPending() {
    if (this.pendingTransfers === 0) {
      this.error = 'No pending transfers to process';
      return;
    }

    this.transferService.processAllPendingTransfers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.showSuccessMessage(`${result.processedCount} transfers processed and auto-assigned`);
          this.loadTransfers(); // Refresh data
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
      'Pending': 'bg-warning',
      'InProgress': 'bg-info',
      'Completed': 'bg-success',
      'Cancelled': 'bg-secondary'
    };
    return statusMap[status] || 'bg-secondary';
  }

  getDetectionBadgeClass(method: string): string {
    const methodMap: { [key: string]: string } = {
      'llm': 'bg-primary',
      'pattern': 'bg-info',
      'keyword': 'bg-secondary'
    };
    return methodMap[method] || 'bg-secondary';
  }

  getReasonDisplay(reason: string): string {
    const reasonMap: { [key: string]: string } = {
      'UserRequested': 'User Requested',
      'EmergencyHandoff': 'Emergency',
      'ComplexityLimit': 'Complexity Limit',
      'QualityAssurance': 'Quality Assurance',
      'SpecialistRequired': 'Specialist Required',
      'SystemEscalation': 'System Escalation'
    };
    return reasonMap[reason] || reason;
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

  transferTrackBy(index: number, transfer: TransferRequest): number {
    return transfer.id;
  }
}