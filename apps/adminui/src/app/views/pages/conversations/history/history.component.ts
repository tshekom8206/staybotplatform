import { Component, OnInit, OnDestroy, inject, ViewChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule, NgbPaginationModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { ConversationHistoryService, ConversationHistory, ConversationHistoryFilters, ConversationHistoryStatistics } from '../../../../core/services/conversation-history.service';
import { ConversationService } from '../../../../core/services/conversation.service';

@Component({
  selector: 'app-conversation-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    NgbPaginationModule,
    FeatherIconDirective
  ],
  template: `
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h4 class="page-title">Conversation History</h4>
      <div class="d-flex gap-2">
        <button class="btn btn-outline-primary btn-sm" (click)="loadHistory()">
          <i feather="refresh-cw" class="me-1"></i>
          Refresh
        </button>
        <button class="btn btn-outline-success btn-sm" (click)="exportHistory()">
          <i feather="download" class="me-1"></i>
          Export
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
            <h5 class="card-title text-primary">{{ totalConversations }}</h5>
            <p class="card-text">Total Conversations</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-success">{{ completedConversations }}</h5>
            <p class="card-text">Completed</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-warning">{{ transferredConversations }}</h5>
            <p class="card-text">Transferred</p>
          </div>
        </div>
      </div>
      <div class="col-md-3">
        <div class="card">
          <div class="card-body text-center">
            <h5 class="card-title text-info">{{ avgSatisfaction }}</h5>
            <p class="card-text">Avg Satisfaction</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Filters Card -->
    <div class="card mb-4">
      <div class="card-body">
        <div class="row g-3">
          <!-- Search -->
          <div class="col-md-4">
            <label class="form-label fw-bold">
              <i feather="search" class="me-1"></i>Search
            </label>
            <input
              type="text"
              class="form-control"
              placeholder="Guest name, phone, room number..."
              [(ngModel)]="searchTerm"
              (input)="applyFilters()">
          </div>

          <!-- Status Filter -->
          <div class="col-md-2">
            <label class="form-label fw-bold">Status</label>
            <select class="form-select" [(ngModel)]="selectedStatus" (change)="applyFilters()">
              <option value="">All Status</option>
              <option value="Completed">Completed</option>
              <option value="Transferred">Transferred</option>
              <option value="Abandoned">Abandoned</option>
            </select>
          </div>

          <!-- Period Filter -->
          <div class="col-md-2">
            <label class="form-label fw-bold">Time Period</label>
            <select class="form-select" [(ngModel)]="selectedPeriod" (change)="onPeriodChange()">
              <option value="">All Time</option>
              <option value="today">Today</option>
              <option value="week">This Week</option>
              <option value="month">This Month</option>
              <option value="custom">Custom Range</option>
            </select>
          </div>

          <!-- Transfer Type Filter -->
          <div class="col-md-2">
            <label class="form-label fw-bold">Type</label>
            <select class="form-select" [(ngModel)]="transferFilter" (change)="applyFilters()">
              <option value="">All Types</option>
              <option value="transferred">Human Agent</option>
              <option value="bot">Bot Only</option>
            </select>
          </div>

          <!-- Items Per Page -->
          <div class="col-md-2">
            <label class="form-label fw-bold">Per Page</label>
            <select class="form-select" [(ngModel)]="itemsPerPage" (change)="applyFilters()">
              <option value="10">10</option>
              <option value="25">25</option>
              <option value="50">50</option>
              <option value="100">100</option>
            </select>
          </div>
        </div>

        <!-- Custom Date Range Row -->
        <div class="row g-3 mt-2" *ngIf="selectedPeriod === 'custom'">
          <div class="col-md-3">
            <label class="form-label fw-bold">
              <i feather="calendar" class="me-1"></i>From Date
            </label>
            <input type="date" class="form-control" [(ngModel)]="dateFrom" (change)="applyFilters()">
          </div>
          <div class="col-md-3">
            <label class="form-label fw-bold">
              <i feather="calendar" class="me-1"></i>To Date
            </label>
            <input type="date" class="form-control" [(ngModel)]="dateTo" (change)="applyFilters()">
          </div>
          <div class="col-md-6 d-flex align-items-end">
            <button class="btn btn-outline-secondary" (click)="clearFilters()">
              <i feather="x" class="me-1"></i>Clear All Filters
            </button>
          </div>
        </div>

        <!-- Clear Filters Button (when not in custom mode) -->
        <div class="row mt-3" *ngIf="selectedPeriod !== 'custom' && (searchTerm || selectedStatus || selectedPeriod || transferFilter)">
          <div class="col-12">
            <button class="btn btn-outline-secondary btn-sm" (click)="clearFilters()">
              <i feather="x" class="me-1"></i>Clear All Filters
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Conversations Table -->
    <div class="card">
      <div class="card-body">
        <!-- Results Summary -->
        <div class="d-flex justify-content-between align-items-center mb-3 pb-3 border-bottom">
          <div>
            <h6 class="mb-0">
              Showing {{ paginatedHistory.length }} of {{ filteredHistory.length }} conversations
              <span class="text-muted" *ngIf="filteredHistory.length !== history.length">
                (filtered from {{ history.length }} total)
              </span>
            </h6>
          </div>
          <div *ngIf="loading">
            <div class="spinner-border spinner-border-sm text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover">
            <thead>
              <tr>
                <th>Guest</th>
                <th>Room</th>
                <th>Started</th>
                <th>Duration</th>
                <th>Messages</th>
                <th>Status</th>
                <th>Agent</th>
                <th>Transfer</th>
                <th>Satisfaction</th>
                <th class="text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let conversation of paginatedHistory; trackBy: conversationTrackBy">
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
                  <div>
                    {{ formatTime(conversation.startedAt) }}
                    <br>
                    <small class="text-muted">
                      {{ conversation.startedAt | date:'short' }}
                    </small>
                  </div>
                </td>
                <td>
                  <span class="badge bg-light text-dark">
                    {{ conversation.duration }}
                  </span>
                </td>
                <td>
                  <span class="badge bg-info">
                    {{ conversation.messageCount }}
                  </span>
                </td>
                <td>
                  <span class="badge" [ngClass]="getStatusBadgeClass(conversation.status)">
                    {{ conversation.status }}
                  </span>
                </td>
                <td>
                  <span *ngIf="conversation.lastAgent" class="badge bg-secondary">
                    {{ conversation.lastAgent }}
                  </span>
                  <span *ngIf="!conversation.lastAgent" class="text-muted">Bot Only</span>
                </td>
                <td>
                  <div *ngIf="conversation.transferredToHuman">
                    <i feather="arrow-right" class="text-warning me-1" ngbTooltip="Transferred to human"></i>
                    <small class="text-muted" *ngIf="conversation.transferReason">
                      {{ getTransferReasonDisplay(conversation.transferReason) }}
                    </small>
                  </div>
                  <span *ngIf="!conversation.transferredToHuman" class="text-muted">-</span>
                </td>
                <td>
                  <div *ngIf="conversation.satisfaction">
                    <div class="d-flex align-items-center">
                      <i feather="star"
                         class="me-1"
                         [class.text-warning]="conversation.satisfaction >= 1"></i>
                      <i feather="star"
                         class="me-1"
                         [class.text-warning]="conversation.satisfaction >= 2"></i>
                      <i feather="star"
                         class="me-1"
                         [class.text-warning]="conversation.satisfaction >= 3"></i>
                      <i feather="star"
                         class="me-1"
                         [class.text-warning]="conversation.satisfaction >= 4"></i>
                      <i feather="star"
                         [class.text-warning]="conversation.satisfaction >= 5"></i>
                      <small class="ms-1">{{ conversation.satisfaction }}/5</small>
                    </div>
                  </div>
                  <span *ngIf="!conversation.satisfaction" class="text-muted">Not rated</span>
                </td>
                <td class="text-end">
                  <div ngbDropdown placement="bottom-end" container="body">
                    <button class="btn btn-outline-primary btn-sm" ngbDropdownToggle>
                      Actions
                    </button>
                    <div class="dropdown-menu dropdown-menu-end" ngbDropdownMenu>
                      <button class="dropdown-item" type="button" (click)="viewConversation(conversation.id)">
                        <i feather="eye" class="me-2"></i>View Details
                      </button>
                      <button class="dropdown-item" type="button" (click)="viewMessages(conversation.id)">
                        <i feather="message-square" class="me-2"></i>View Messages
                      </button>
                      <button class="dropdown-item" type="button" (click)="exportConversation(conversation.id)">
                        <i feather="download" class="me-2"></i>Export
                      </button>
                      <div class="dropdown-divider" *ngIf="conversation.transferredToHuman"></div>
                      <button class="dropdown-item" type="button" (click)="viewTransferDetails(conversation.id)"
                         *ngIf="conversation.transferredToHuman">
                        <i feather="arrow-right" class="me-2"></i>Transfer Details
                      </button>
                    </div>
                  </div>
                </td>
              </tr>
              <tr *ngIf="paginatedHistory.length === 0">
                <td colspan="10" class="text-center text-muted py-4">
                  <i feather="message-circle" class="me-2"></i>
                  No conversation history found
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        <div class="d-flex justify-content-between align-items-center mt-4 pt-3 border-top" *ngIf="filteredHistory.length > 0">
          <div class="text-muted small">
            Page {{ currentPage }} of {{ totalPages }}
          </div>
          <ngb-pagination
            [(page)]="currentPage"
            [pageSize]="itemsPerPage"
            [collectionSize]="filteredHistory.length"
            [maxSize]="5"
            [boundaryLinks]="true"
            [rotate]="true"
            (pageChange)="updatePagination()">
          </ngb-pagination>
          <div class="text-muted small">
            {{ filteredHistory.length }} total result{{ filteredHistory.length !== 1 ? 's' : '' }}
          </div>
        </div>

        <!-- No Results Message -->
        <div class="text-center py-5" *ngIf="filteredHistory.length === 0 && !loading">
          <i feather="inbox" class="icon-xl text-muted mb-3"></i>
          <h5 class="text-muted">No conversations found</h5>
          <p class="text-muted">Try adjusting your filters to see more results</p>
          <button class="btn btn-outline-primary btn-sm" (click)="clearFilters()">
            <i feather="refresh-cw" class="me-1"></i>Clear Filters
          </button>
        </div>
      </div>
    </div>

    <!-- Conversation Details Modal -->
    <ng-template #detailsModal let-modal>
      <div class="modal-header bg-primary text-white">
        <h5 class="modal-title">
          <i feather="info" class="me-2"></i>
          Conversation Summary
        </h5>
        <button type="button" class="btn-close btn-close-white" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" *ngIf="selectedConversationDetails">
        <div class="row g-3">
          <div class="col-md-6">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Guest Information</h6>
                <p class="mb-1"><strong>Name:</strong> {{ selectedConversationDetails.guestName || 'Guest' }}</p>
                <p class="mb-1"><strong>Phone:</strong> {{ selectedConversationDetails.guestPhone }}</p>
                <p class="mb-0"><strong>Room:</strong> {{ selectedConversationDetails.roomNumber || 'Not assigned' }}</p>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Conversation Details</h6>
                <p class="mb-1"><strong>Status:</strong>
                  <span class="badge" [ngClass]="getStatusBadgeClass(selectedConversationDetails.status)">
                    {{ selectedConversationDetails.status }}
                  </span>
                </p>
                <p class="mb-1"><strong>Started:</strong> {{ selectedConversationDetails.createdAt | date:'short' }}</p>
                <p class="mb-0"><strong>Duration:</strong> {{ calculateDuration(selectedConversationDetails.createdAt, selectedConversationDetails.endedAt) }}</p>
              </div>
            </div>
          </div>
          <div class="col-12">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Activity Summary</h6>
                <p class="mb-1"><strong>Total Messages:</strong> {{ selectedConversationDetails.messageCount }}</p>
                <p class="mb-1"><strong>Handled By:</strong> {{ selectedConversationDetails.assignedAgent || 'Chatbot' }}</p>
                <p class="mb-0"><strong>Last Activity:</strong> {{ selectedConversationDetails.lastActivity | date:'short' }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.close()">Close</button>
      </div>
    </ng-template>

    <!-- Messages Modal -->
    <ng-template #messagesModal let-modal>
      <div class="modal-header bg-primary text-white">
        <h5 class="modal-title">
          <i feather="message-square" class="me-2"></i>
          Conversation Messages
        </h5>
        <button type="button" class="btn-close btn-close-white" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" style="max-height: 500px; overflow-y: auto;">
        <div class="chat-container">
          <div *ngFor="let message of selectedMessages" class="message-row mb-3"
               [class.guest-message]="message.senderType === 'Guest'"
               [class.bot-message]="message.senderType !== 'Guest'">
            <div class="message-bubble p-3 rounded"
                 [class.bg-light]="message.senderType === 'Guest'"
                 [class.bg-primary]="message.senderType !== 'Guest'"
                 [class.text-white]="message.senderType !== 'Guest'">
              <div class="d-flex justify-content-between align-items-start mb-2">
                <strong class="sender-name">
                  <i feather="user" class="me-1" *ngIf="message.senderType === 'Guest'"></i>
                  <i feather="cpu" class="me-1" *ngIf="message.senderType !== 'Guest'"></i>
                  {{ message.senderName }}
                </strong>
                <small class="text-muted" *ngIf="message.senderType === 'Guest'">
                  {{ message.timestamp | date:'short' }}
                </small>
                <small class="text-white-50" *ngIf="message.senderType !== 'Guest'">
                  {{ message.timestamp | date:'short' }}
                </small>
              </div>
              <p class="mb-0">{{ message.messageText }}</p>
            </div>
          </div>
          <div *ngIf="selectedMessages.length === 0" class="text-center text-muted py-4">
            <i feather="message-circle" class="me-2"></i>
            No messages found
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.close()">Close</button>
      </div>
    </ng-template>

    <!-- Transfer Details Modal -->
    <ng-template #transferModal let-modal>
      <div class="modal-header bg-warning text-dark">
        <h5 class="modal-title">
          <i feather="arrow-right" class="me-2"></i>
          Transfer Information
        </h5>
        <button type="button" class="btn-close" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body" *ngIf="selectedTransferDetails">
        <div class="alert alert-info">
          <i feather="info" class="me-2"></i>
          This conversation was transferred to a staff member
        </div>

        <div class="row g-3">
          <div class="col-md-6">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Transfer Details</h6>
                <p class="mb-1"><strong>Reason:</strong> {{ selectedTransferDetails.transferReason }}</p>
                <p class="mb-1"><strong>Type:</strong>
                  <span class="badge bg-info">{{ selectedTransferDetails.transferType }}</span>
                </p>
                <p class="mb-0"><strong>When:</strong> {{ selectedTransferDetails.transferredAt | date:'short' }}</p>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Staff Assignment</h6>
                <p class="mb-1"><strong>From:</strong> {{ selectedTransferDetails.fromAgent }}</p>
                <p class="mb-1"><strong>To:</strong> {{ selectedTransferDetails.toAgent }}</p>
                <p class="mb-0"><strong>Status:</strong> {{ selectedTransferDetails.conversationStatus }}</p>
              </div>
            </div>
          </div>
          <div class="col-12" *ngIf="selectedTransferDetails.transferSummary">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-2">Summary</h6>
                <p class="mb-0">{{ selectedTransferDetails.transferSummary }}</p>
              </div>
            </div>
          </div>
          <div class="col-12" *ngIf="selectedTransferDetails.recentMessages && selectedTransferDetails.recentMessages.length > 0">
            <div class="card border-0 bg-light">
              <div class="card-body">
                <h6 class="text-muted mb-3">Recent Context (Last messages before transfer)</h6>
                <div class="message-preview" *ngFor="let msg of selectedTransferDetails.recentMessages">
                  <div class="d-flex align-items-start mb-2">
                    <i feather="user" class="me-2 mt-1" *ngIf="msg.isFromGuest"></i>
                    <i feather="cpu" class="me-2 mt-1" *ngIf="!msg.isFromGuest"></i>
                    <div class="flex-grow-1">
                      <small class="text-muted">{{ msg.sender }} - {{ msg.timestamp | date:'short' }}</small>
                      <p class="mb-0">{{ msg.text }}</p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" (click)="modal.close()">Close</button>
      </div>
    </ng-template>
  `,
  styleUrls: ['./history.component.scss']
})
export class ConversationHistoryComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private historyService = inject(ConversationHistoryService);
  private conversationService = inject(ConversationService);
  private modalService = inject(NgbModal);

  // Modal template references
  @ViewChild('detailsModal') detailsModal!: TemplateRef<any>;
  @ViewChild('messagesModal') messagesModal!: TemplateRef<any>;
  @ViewChild('transferModal') transferModal!: TemplateRef<any>;

  // Data properties
  history: ConversationHistory[] = [];
  filteredHistory: ConversationHistory[] = [];
  paginatedHistory: ConversationHistory[] = [];

  // Modal data
  selectedConversationDetails: any = null;
  selectedMessages: any[] = [];
  selectedTransferDetails: any = null;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedStatus = '';
  selectedPeriod = '';
  transferFilter = '';
  dateFrom: string = '';
  dateTo: string = '';

  // Pagination
  currentPage = 1;
  itemsPerPage = 25;
  totalPages = 1;

  // Statistics
  totalConversations = 0;
  completedConversations = 0;
  transferredConversations = 0;
  avgSatisfaction = '0.0';

  // Math reference for template
  Math = Math;

  ngOnInit() {
    this.loadHistory();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadHistory() {
    this.loading = true;
    this.error = null;

    const filters = this.buildFilters();

    // Load conversation history from database
    this.historyService.getConversationHistory(filters, this.currentPage, this.itemsPerPage)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (history) => {
          this.history = history;
          this.applyFilters();
          this.loadStatistics();
          this.loading = false;
        },
        error: (error) => {
          this.error = error.message;
          this.loading = false;
          console.error('Error loading conversation history:', error);
        }
      });
  }

  applyFilters() {
    let filtered = [...this.history];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(conv =>
        conv.guestName?.toLowerCase().includes(term) ||
        conv.guestPhone.toLowerCase().includes(term) ||
        conv.roomNumber?.toLowerCase().includes(term) ||
        conv.lastAgent?.toLowerCase().includes(term)
      );
    }

    // Apply status filter
    if (this.selectedStatus) {
      filtered = filtered.filter(conv => conv.status === this.selectedStatus);
    }

    // Apply transfer filter
    if (this.transferFilter === 'transferred') {
      filtered = filtered.filter(conv => conv.transferredToHuman);
    } else if (this.transferFilter === 'bot') {
      filtered = filtered.filter(conv => !conv.transferredToHuman);
    }

    // Apply period filter
    if (this.selectedPeriod) {
      const now = new Date();
      let startDate: Date;
      let endDate: Date = new Date(now.getTime() + 24 * 60 * 60 * 1000); // Tomorrow

      switch (this.selectedPeriod) {
        case 'today':
          startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
          break;
        case 'week':
          startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
          break;
        case 'month':
          startDate = new Date(now.getFullYear(), now.getMonth(), 1);
          break;
        case 'custom':
          if (this.dateFrom) {
            startDate = new Date(this.dateFrom);
          } else {
            startDate = new Date(0);
          }
          if (this.dateTo) {
            endDate = new Date(this.dateTo);
            endDate.setHours(23, 59, 59, 999); // End of day
          }
          break;
        default:
          startDate = new Date(0);
      }

      filtered = filtered.filter(conv => {
        const convDate = new Date(conv.startedAt);
        return convDate >= startDate && convDate <= endDate;
      });
    }

    // Sort by start time (newest first)
    filtered.sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime());

    this.filteredHistory = filtered;
    this.totalPages = Math.ceil(filtered.length / this.itemsPerPage);
    this.currentPage = 1; // Reset to first page when filters change
    this.updatePagination();
  }

  updatePagination() {
    const startIndex = (this.currentPage - 1) * this.itemsPerPage;
    const endIndex = startIndex + this.itemsPerPage;
    this.paginatedHistory = this.filteredHistory.slice(startIndex, endIndex);
  }

  loadStatistics() {
    const filters = this.buildFilters();

    this.historyService.getHistoryStatistics(filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.totalConversations = stats.totalConversations;
          this.completedConversations = stats.completedConversations;
          this.transferredConversations = stats.transferredConversations;
          this.avgSatisfaction = stats.avgSatisfaction.toFixed(1);
        },
        error: (error) => {
          console.error('Error loading history statistics:', error);
          // Fallback to calculated stats from loaded history
          this.updateLocalStatistics();
        }
      });
  }

  updateLocalStatistics() {
    this.totalConversations = this.history.length;
    this.completedConversations = this.history.filter(h => h.status === 'Completed').length;
    this.transferredConversations = this.history.filter(h => h.transferredToHuman).length;

    const ratingsWithValue = this.history.filter(h => h.satisfaction && h.satisfaction > 0);
    if (ratingsWithValue.length > 0) {
      const avgRating = ratingsWithValue.reduce((sum, h) => sum + h.satisfaction!, 0) / ratingsWithValue.length;
      this.avgSatisfaction = avgRating.toFixed(1);
    } else {
      this.avgSatisfaction = '0.0';
    }
  }

  buildFilters(): ConversationHistoryFilters {
    const filters: ConversationHistoryFilters = {};

    if (this.selectedStatus) {
      filters.status = this.selectedStatus;
    }

    if (this.transferFilter) {
      filters.transferFilter = this.transferFilter;
    }

    if (this.selectedPeriod) {
      filters.period = this.selectedPeriod;
    }

    return filters;
  }

  onPeriodChange() {
    if (this.selectedPeriod !== 'custom') {
      this.dateFrom = '';
      this.dateTo = '';
    }
    this.applyFilters();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedStatus = '';
    this.selectedPeriod = '';
    this.transferFilter = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.applyFilters();
  }

  viewConversation(conversationId: number) {
    this.loading = true;
    this.conversationService.getConversationDetails(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (details) => {
          this.selectedConversationDetails = details;
          this.loading = false;
          this.modalService.open(this.detailsModal, { size: 'lg', centered: true });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  viewMessages(conversationId: number) {
    this.loading = true;
    this.conversationService.getConversationMessages(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (messages) => {
          this.selectedMessages = messages;
          this.loading = false;
          this.modalService.open(this.messagesModal, { size: 'lg', centered: true, scrollable: true });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  viewTransferDetails(conversationId: number) {
    this.loading = true;
    this.historyService.getConversationTransferDetails(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (transferDetails) => {
          this.selectedTransferDetails = transferDetails;
          this.loading = false;
          this.modalService.open(this.transferModal, { size: 'lg', centered: true });
        },
        error: (error) => {
          this.loading = false;
          this.error = error.message;
        }
      });
  }

  exportConversation(conversationId: number) {
    this.historyService.exportSingleConversation(conversationId, 'pdf')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const filename = `conversation-${conversationId}-${new Date().toISOString().split('T')[0]}.pdf`;
          this.historyService.downloadFile(blob, filename);
          this.showSuccessMessage('Conversation exported successfully');
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  exportHistory() {
    const filters = this.buildFilters();

    this.historyService.exportConversationHistory('excel', filters)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const filename = `conversation-history-${new Date().toISOString().split('T')[0]}.xlsx`;
          this.historyService.downloadFile(blob, filename);
          this.showSuccessMessage('History exported successfully');
        },
        error: (error) => {
          this.error = error.message;
        }
      });
  }

  calculateDuration(startDate: any, endDate: any): string {
    if (!startDate) return 'N/A';

    const start = new Date(startDate);
    const end = endDate ? new Date(endDate) : new Date();
    const durationMs = end.getTime() - start.getTime();
    const minutes = Math.floor(durationMs / 60000);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    }
    return `${minutes}m`;
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
      'Completed': 'bg-success',
      'Transferred': 'bg-warning',
      'Abandoned': 'bg-danger'
    };
    return statusMap[status] || 'bg-secondary';
  }

  getTransferReasonDisplay(reason: string): string {
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
    const hours = Math.floor(diff / (60 * 60 * 1000));
    const days = Math.floor(hours / 24);

    if (days > 0) return `${days}d ago`;
    if (hours > 0) return `${hours}h ago`;

    const minutes = Math.floor(diff / (60 * 1000));
    if (minutes > 0) return `${minutes}m ago`;

    return 'Just now';
  }

  conversationTrackBy(index: number, conversation: ConversationHistory): number {
    return conversation.id;
  }
}