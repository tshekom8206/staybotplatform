import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbModalModule, NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { BroadcastService, BroadcastHistoryItem } from '../../../../core/services/broadcast.service';

export interface MessageAnalytics {
  totalMessages: number;
  totalRecipients: number;
  averageDeliveryRate: number;
  averageReadRate: number;
  topPerformingTemplate: string;
  messageTrends: {
    date: string;
    sent: number;
    delivered: number;
    read: number;
  }[];
}

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbModalModule,
    NgbPaginationModule,
    FeatherIconDirective
  ],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss'
})
export class HistoryComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private modalService = inject(NgbModal);
  private broadcastService = inject(BroadcastService);

  // Data properties
  messageHistory: BroadcastHistoryItem[] = [];
  filteredHistory: BroadcastHistoryItem[] = [];
  analytics: MessageAnalytics | null = null;
  loading = true;
  error: string | null = null;

  // Filter properties
  searchTerm = '';
  typeFilter: 'all' | 'general' | 'emergency' | 'template' = 'all';
  statusFilter: 'all' | 'Completed' | 'Failed' | 'Pending' | 'InProgress' = 'all';
  dateRange = '30'; // days
  selectedMessage: BroadcastHistoryItem | null = null;

  // Pagination properties
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Sort properties
  sortField: keyof BroadcastHistoryItem = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Utility properties
  Math = Math;

  ngOnInit(): void {
    this.loadMessageHistory();
    this.loadAnalytics();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadMessageHistory(): void {
    this.loading = true;
    this.error = null;

    this.broadcastService.getBroadcastHistory(50)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.loading = false;
          if (response.success && response.data) {
            this.messageHistory = response.data;
            this.applyFilters();
          } else {
            this.error = response.error || 'Failed to load broadcast history';
          }
        },
        error: (error) => {
          this.loading = false;
          this.error = 'Failed to load broadcast history';
          console.error('Error loading broadcast history:', error);
        }
      });
  }

  private loadAnalytics(): void {
    // Simulate loading analytics
    setTimeout(() => {
      this.analytics = {
        totalMessages: 45,
        totalRecipients: 6780,
        averageDeliveryRate: 97.2,
        averageReadRate: 68.5,
        topPerformingTemplate: 'Welcome Message',
        messageTrends: [
          { date: '2024-01-20', sent: 3, delivered: 456, read: 342 },
          { date: '2024-01-21', sent: 5, delivered: 678, read: 502 },
          { date: '2024-01-22', sent: 2, delivered: 234, read: 187 },
          { date: '2024-01-23', sent: 4, delivered: 567, read: 398 },
          { date: '2024-01-24', sent: 6, delivered: 890, read: 623 },
          { date: '2024-01-25', sent: 3, delivered: 456, read: 312 }
        ]
      };
    }, 500);
  }

  applyFilters(): void {
    let filtered = [...this.messageHistory];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(msg =>
        msg.messageType.toLowerCase().includes(term) ||
        msg.createdBy.toLowerCase().includes(term)
      );
    }

    // Type filter
    if (this.typeFilter !== 'all') {
      filtered = filtered.filter(msg => msg.messageType === this.typeFilter);
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(msg => msg.status === this.statusFilter);
    }

    // Date range filter
    if (this.dateRange !== 'all') {
      const days = parseInt(this.dateRange);
      const cutoffDate = new Date();
      cutoffDate.setDate(cutoffDate.getDate() - days);
      filtered = filtered.filter(msg => new Date(msg.createdAt) >= cutoffDate);
    }

    // Apply sorting
    filtered = this.sortMessages(filtered);

    this.filteredHistory = filtered;
    this.totalItems = filtered.length;
  }

  private sortMessages(messages: BroadcastHistoryItem[]): BroadcastHistoryItem[] {
    return messages.sort((a, b) => {
      const aVal = a[this.sortField];
      const bVal = b[this.sortField];

      let comparison = 0;

      if (typeof aVal === 'string' && typeof bVal === 'string') {
        // Handle date strings
        if (this.sortField === 'createdAt' || this.sortField === 'completedAt') {
          const dateA = new Date(aVal);
          const dateB = new Date(bVal);
          comparison = dateA.getTime() - dateB.getTime();
        } else {
          comparison = aVal.localeCompare(bVal);
        }
      } else if (typeof aVal === 'number' && typeof bVal === 'number') {
        comparison = aVal - bVal;
      }

      return this.sortDirection === 'desc' ? -comparison : comparison;
    });
  }

  sort(field: keyof BroadcastHistoryItem): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'desc';
    }
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.typeFilter = 'all';
    this.statusFilter = 'all';
    this.dateRange = '30';
    this.currentPage = 1;
    this.applyFilters();
  }

  getTypeClass(type: string): string {
    switch (type) {
      case 'emergency': return 'badge bg-danger';
      case 'broadcast': return 'badge bg-primary';
      case 'template': return 'badge bg-info';
      default: return 'badge bg-secondary';
    }
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'emergency': return 'alert-triangle';
      case 'broadcast': return 'radio';
      case 'template': return 'file-text';
      default: return 'message-square';
    }
  }

  getPriorityClass(priority: string): string {
    switch (priority) {
      case 'urgent': return 'badge bg-danger';
      case 'high': return 'badge bg-warning';
      case 'medium': return 'badge bg-info';
      case 'low': return 'badge bg-secondary';
      default: return 'badge bg-secondary';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'sent': return 'badge bg-success';
      case 'failed': return 'badge bg-danger';
      case 'scheduled': return 'badge bg-warning';
      case 'cancelled': return 'badge bg-secondary';
      default: return 'badge bg-secondary';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'sent': return 'check-circle';
      case 'failed': return 'x-circle';
      case 'scheduled': return 'clock';
      case 'cancelled': return 'slash';
      default: return 'circle';
    }
  }

  getDeliveryRate(message: BroadcastHistoryItem): number {
    if (message.totalRecipients === 0) return 0;
    return Math.round((message.successfulDeliveries / message.totalRecipients) * 100);
  }

  getReadRate(message: BroadcastHistoryItem): number {
    // Since we don't have read count in BroadcastHistoryItem, return N/A or assume 100% of delivered messages are read
    return message.successfulDeliveries > 0 ? 100 : 0;
  }

  getAcknowledgmentRate(message: BroadcastHistoryItem): number {
    // Since we don't have acknowledgment count in BroadcastHistoryItem, return N/A
    return 0;
  }

  getTimeAgo(date: string): string {
    const now = new Date();
    const messageDate = new Date(date);
    const diffInMinutes = Math.floor((now.getTime() - messageDate.getTime()) / (1000 * 60));

    if (diffInMinutes < 1) return 'Just now';
    if (diffInMinutes < 60) return `${diffInMinutes}m ago`;

    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) return `${diffInHours}h ago`;

    const diffInDays = Math.floor(diffInHours / 24);
    if (diffInDays < 7) return `${diffInDays}d ago`;

    return messageDate.toLocaleDateString();
  }

  viewMessage(content: any, message: BroadcastHistoryItem): void {
    this.selectedMessage = message;
    this.modalService.open(content, { size: 'lg' });
  }

  exportHistory(): void {
    // Simulate export functionality
    console.log('Exporting message history...');
    // TODO: Implement actual export functionality
  }

  get paginatedHistory(): BroadcastHistoryItem[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return this.filteredHistory.slice(startIndex, endIndex);
  }

  get totalPages(): number {
    return Math.ceil(this.totalItems / this.pageSize);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  trackByMessageId(index: number, message: BroadcastHistoryItem): number {
    return message.id;
  }

  refresh(): void {
    this.loadMessageHistory();
    this.loadAnalytics();
  }
}