import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { GuestService, GuestConversationSummary } from '../../../../core/services/guest.service';
import { SignalRService } from '../../../../core/services/signalr.service';

@Component({
  selector: 'app-conversations',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    FeatherIconDirective
  ],
  templateUrl: './conversations.component.html',
  styleUrl: './conversations.component.scss'
})
export class ConversationsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private guestService = inject(GuestService);
  private signalRService = inject(SignalRService);

  // Data properties
  conversations: GuestConversationSummary[] = [];
  filteredConversations: GuestConversationSummary[] = [];
  loading = true;
  error: string | null = null;

  // Filter properties
  searchTerm = '';
  statusFilter: 'all' | 'active' | 'pending' | 'closed' = 'all';
  priorityFilter: 'all' | 'urgent' | 'high' | 'medium' | 'low' = 'all';
  roomFilter = '';

  // Pagination properties
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Sort properties
  sortField: keyof GuestConversationSummary = 'lastMessageAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Quick reply properties
  quickReplyConversationId: number | null = null;
  quickReplyMessage = '';
  sendingQuickReply = false;

  ngOnInit(): void {
    this.loadConversations();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadConversations(): void {
    this.loading = true;
    this.error = null;

    this.guestService.getActiveConversations()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (conversations) => {
          this.conversations = conversations;
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading conversations:', error);
          this.error = 'Failed to load conversations. Please try again.';
          this.loading = false;
        }
      });
  }

  private setupRealTimeUpdates(): void {
    // Listen for new messages
    this.signalRService.messageReceived$
      .pipe(takeUntil(this.destroy$))
      .subscribe((message) => {
        this.refreshConversationData();
      });

    // Listen for status changes
    this.signalRService.conversationStatusChanged$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshConversationData();
      });
  }

  private refreshConversationData(): void {
    // Refresh data without showing loading state
    this.guestService.getActiveConversations()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (conversations) => {
          this.conversations = conversations;
          this.applyFilters();
        },
        error: (error) => {
          console.error('Error refreshing conversations:', error);
        }
      });
  }

  applyFilters(): void {
    let filtered = [...this.conversations];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(conv =>
        conv.guestName.toLowerCase().includes(term) ||
        conv.phoneNumber.includes(term) ||
        conv.roomNumber?.toLowerCase().includes(term) ||
        conv.lastMessage.toLowerCase().includes(term)
      );
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(conv =>
        conv.status.toLowerCase() === this.statusFilter
      );
    }

    // Priority filter
    if (this.priorityFilter !== 'all') {
      filtered = filtered.filter(conv =>
        conv.priority === this.priorityFilter
      );
    }

    // Room filter
    if (this.roomFilter) {
      filtered = filtered.filter(conv =>
        conv.roomNumber?.includes(this.roomFilter)
      );
    }

    // Apply sorting
    filtered = this.sortConversations(filtered);

    this.filteredConversations = filtered;
    this.totalItems = filtered.length;
  }

  private sortConversations(conversations: GuestConversationSummary[]): GuestConversationSummary[] {
    return conversations.sort((a, b) => {
      const aVal = a[this.sortField];
      const bVal = b[this.sortField];

      let comparison = 0;

      if (aVal instanceof Date && bVal instanceof Date) {
        comparison = aVal.getTime() - bVal.getTime();
      } else if (typeof aVal === 'string' && typeof bVal === 'string') {
        comparison = aVal.localeCompare(bVal);
      } else if (typeof aVal === 'number' && typeof bVal === 'number') {
        comparison = aVal - bVal;
      }

      return this.sortDirection === 'desc' ? -comparison : comparison;
    });
  }

  sort(field: keyof GuestConversationSummary): void {
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
    this.statusFilter = 'all';
    this.priorityFilter = 'all';
    this.roomFilter = '';
    this.currentPage = 1;
    this.applyFilters();
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
    switch (status.toLowerCase()) {
      case 'active': return 'badge bg-success';
      case 'pending': return 'badge bg-warning';
      case 'closed': return 'badge bg-secondary';
      default: return 'badge bg-secondary';
    }
  }

  getTimeAgo(date: Date | string): string {
    const now = new Date();
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const diffInMinutes = Math.floor((now.getTime() - dateObj.getTime()) / (1000 * 60));

    if (diffInMinutes < 1) return 'Just now';
    if (diffInMinutes < 60) return `${diffInMinutes}m ago`;

    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) return `${diffInHours}h ago`;

    const diffInDays = Math.floor(diffInHours / 24);
    return `${diffInDays}d ago`;
  }

  openConversation(conversation: GuestConversationSummary): void {
    // Navigate to conversation detail view
    console.log('Opening conversation:', conversation.conversationId);
    // TODO: Implement navigation to conversation detail
  }

  markAsResolved(conversationId: number, event: Event): void {
    event.stopPropagation();

    this.guestService.updateConversationStatus(conversationId, 'Closed')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshConversationData();
        },
        error: (error) => {
          console.error('Error updating conversation status:', error);
        }
      });
  }

  sendQuickReply(conversation: GuestConversationSummary, event: Event): void {
    event.stopPropagation();

    if (this.quickReplyConversationId === conversation.conversationId) {
      // Close quick reply input if already open for this conversation
      this.quickReplyConversationId = null;
      this.quickReplyMessage = '';
    } else {
      // Open quick reply input for this conversation
      this.quickReplyConversationId = conversation.conversationId;
      this.quickReplyMessage = '';
    }
  }

  submitQuickReply(conversation: GuestConversationSummary): void {
    if (!this.quickReplyMessage.trim() || this.sendingQuickReply) {
      return;
    }

    this.sendingQuickReply = true;

    this.guestService.sendMessage(conversation.phoneNumber, this.quickReplyMessage.trim(), conversation.conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.quickReplyConversationId = null;
          this.quickReplyMessage = '';
          this.sendingQuickReply = false;
          this.refreshConversationData();
        },
        error: (error) => {
          console.error('Error sending quick reply:', error);
          this.sendingQuickReply = false;
          // You could show a toast notification here
        }
      });
  }

  cancelQuickReply(): void {
    this.quickReplyConversationId = null;
    this.quickReplyMessage = '';
  }

  get paginatedConversations(): GuestConversationSummary[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return this.filteredConversations.slice(startIndex, endIndex);
  }

  get totalPages(): number {
    return Math.ceil(this.totalItems / this.pageSize);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  refresh(): void {
    this.loadConversations();
  }

  // Make Math available to template
  Math = Math;
}