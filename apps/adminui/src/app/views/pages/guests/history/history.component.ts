import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbDatepickerModule, NgbCalendar } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { GuestService, GuestHistorySummary } from '../../../../core/services/guest.service';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    NgbDatepickerModule,
    FeatherIconDirective
  ],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss'
})
export class HistoryComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private guestService = inject(GuestService);
  private calendar = inject(NgbCalendar);
  private router = inject(Router);

  // Data properties
  history: GuestHistorySummary[] = [];
  filteredHistory: GuestHistorySummary[] = [];
  loading = true;
  error: string | null = null;

  // Statistics
  stats = {
    total: 0,
    resolved: 0,
    pending: 0,
    escalated: 0,
    avgResolutionTime: '0m',
    mostCommonType: 'chat'
  };

  // Filter properties
  searchTerm = '';
  typeFilter: 'all' | 'chat' | 'task' | 'emergency' | 'complaint' | 'request' = 'all';
  statusFilter: 'all' | 'resolved' | 'pending' | 'escalated' = 'all';
  staffFilter = '';
  roomFilter = '';

  // Date range filters
  dateFrom = this.calendar.getPrev(this.calendar.getToday(), 'd', 30); // 30 days ago
  dateTo = this.calendar.getToday();

  // Pagination properties
  currentPage = 1;
  pageSize = 15;
  totalItems = 0;

  // Sort properties
  sortField: keyof GuestHistorySummary = 'date';
  sortDirection: 'asc' | 'desc' = 'desc';

  ngOnInit(): void {
    this.loadHistory();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadHistory(): void {
    this.loading = true;
    this.error = null;

    this.guestService.getGuestHistory(this.currentPage, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (history) => {
          this.history = history;
          this.calculateStats();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading history:', error);
          this.error = 'Failed to load guest history. Please try again.';
          this.loading = false;
        }
      });
  }

  private calculateStats(): void {
    const total = this.history.length;
    const resolved = this.history.filter(h => h.status === 'resolved').length;
    const pending = this.history.filter(h => h.status === 'pending').length;
    const escalated = this.history.filter(h => h.status === 'escalated').length;

    // Calculate average resolution time from duration strings
    const resolvedWithDuration = this.history.filter(h => h.status === 'resolved' && h.duration);
    const totalMinutes = resolvedWithDuration.reduce((sum, h) => {
      const match = h.duration?.match(/(\d+)/);
      return sum + (match ? parseInt(match[1]) : 0);
    }, 0);
    const avgMinutes = resolvedWithDuration.length > 0 ? Math.round(totalMinutes / resolvedWithDuration.length) : 0;

    // Find most common interaction type
    const typeCounts: { [key: string]: number } = {};
    this.history.forEach(h => {
      typeCounts[h.interactionType] = (typeCounts[h.interactionType] || 0) + 1;
    });
    const mostCommonType = Object.keys(typeCounts).reduce((a, b) =>
      typeCounts[a] > typeCounts[b] ? a : b, 'chat'
    );

    this.stats = {
      total,
      resolved,
      pending,
      escalated,
      avgResolutionTime: avgMinutes > 60 ? `${Math.round(avgMinutes / 60)}h` : `${avgMinutes}m`,
      mostCommonType
    };
  }

  applyFilters(): void {
    let filtered = [...this.history];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(item =>
        item.guestName.toLowerCase().includes(term) ||
        item.roomNumber.toLowerCase().includes(term) ||
        item.staffMember.toLowerCase().includes(term) ||
        item.summary.toLowerCase().includes(term)
      );
    }

    // Type filter
    if (this.typeFilter !== 'all') {
      filtered = filtered.filter(item => item.interactionType === this.typeFilter);
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status === this.statusFilter);
    }

    // Staff filter
    if (this.staffFilter) {
      filtered = filtered.filter(item =>
        item.staffMember.toLowerCase().includes(this.staffFilter.toLowerCase())
      );
    }

    // Room filter
    if (this.roomFilter) {
      filtered = filtered.filter(item =>
        item.roomNumber.includes(this.roomFilter)
      );
    }

    // Date range filter
    const fromDate = new Date(this.dateFrom.year, this.dateFrom.month - 1, this.dateFrom.day);
    const toDate = new Date(this.dateTo.year, this.dateTo.month - 1, this.dateTo.day, 23, 59, 59);

    filtered = filtered.filter(item => {
      const itemDate = new Date(item.date);
      return itemDate >= fromDate && itemDate <= toDate;
    });

    // Apply sorting
    filtered = this.sortHistory(filtered);

    this.filteredHistory = filtered;
    this.totalItems = filtered.length;
  }

  private sortHistory(history: GuestHistorySummary[]): GuestHistorySummary[] {
    return history.sort((a, b) => {
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

  sort(field: keyof GuestHistorySummary): void {
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
    this.staffFilter = '';
    this.roomFilter = '';
    this.dateFrom = this.calendar.getPrev(this.calendar.getToday(), 'd', 30);
    this.dateTo = this.calendar.getToday();
    this.currentPage = 1;
    this.applyFilters();
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'chat': return 'message-square';
      case 'task': return 'clipboard';
      case 'emergency': return 'alert-triangle';
      case 'complaint': return 'frown';
      case 'request': return 'help-circle';
      default: return 'activity';
    }
  }

  getTypeClass(type: string): string {
    switch (type) {
      case 'chat': return 'badge bg-primary';
      case 'task': return 'badge bg-info';
      case 'emergency': return 'badge bg-danger';
      case 'complaint': return 'badge bg-warning text-dark';
      case 'request': return 'badge bg-success';
      default: return 'badge bg-secondary';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'resolved': return 'badge bg-success';
      case 'pending': return 'badge bg-warning text-dark';
      case 'escalated': return 'badge bg-danger';
      default: return 'badge bg-secondary';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'resolved': return 'check-circle';
      case 'pending': return 'clock';
      case 'escalated': return 'alert-circle';
      default: return 'help-circle';
    }
  }

  getRelativeDate(date: Date | string): string {
    const now = new Date();
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const diffInHours = Math.floor((now.getTime() - dateObj.getTime()) / (1000 * 60 * 60));

    if (diffInHours < 1) {
      const diffInMinutes = Math.floor((now.getTime() - dateObj.getTime()) / (1000 * 60));
      return diffInMinutes <= 1 ? 'Just now' : `${diffInMinutes} min ago`;
    } else if (diffInHours < 24) {
      return `${diffInHours}h ago`;
    } else {
      const diffInDays = Math.floor(diffInHours / 24);
      if (diffInDays === 1) return 'Yesterday';
      if (diffInDays < 7) return `${diffInDays} days ago`;
      return dateObj.toLocaleDateString();
    }
  }

  viewDetails(item: GuestHistorySummary): void {
    this.router.navigate(['/guests/interaction', item.id]);
  }

  exportData(): void {
    console.log('Exporting history data...');
    // TODO: Implement data export functionality
  }

  get paginatedHistory(): GuestHistorySummary[] {
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

  refresh(): void {
    this.loadHistory();
  }

  // Make Math available to template
  Math = Math;
}