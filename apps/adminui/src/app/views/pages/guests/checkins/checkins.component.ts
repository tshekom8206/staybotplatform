import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbDropdownModule, NgbPaginationModule, NgbTooltipModule, NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { GuestService, CheckinSummary } from '../../../../core/services/guest.service';
import { SignalRService } from '../../../../core/services/signalr.service';

@Component({
  selector: 'app-checkins',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgbDropdownModule,
    NgbPaginationModule,
    NgbTooltipModule,
    NgbProgressbarModule,
    FeatherIconDirective
  ],
  templateUrl: './checkins.component.html',
  styleUrl: './checkins.component.scss'
})
export class CheckinsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private guestService = inject(GuestService);
  private signalRService = inject(SignalRService);

  // Data properties
  checkins: CheckinSummary[] = [];
  filteredCheckins: CheckinSummary[] = [];
  loading = true;
  error: string | null = null;

  // Statistics
  stats = {
    total: 0,
    completed: 0,
    pending: 0,
    inProgress: 0,
    noShow: 0,
    completionRate: 0
  };

  // Filter properties
  searchTerm = '';
  statusFilter: 'all' | 'pending' | 'in-progress' | 'completed' | 'no-show' = 'all';
  timeFilter: 'all' | 'overdue' | 'upcoming' | 'today' = 'today';

  // Pagination properties
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Sort properties
  sortField: keyof CheckinSummary = 'expectedArrival';
  sortDirection: 'asc' | 'desc' = 'asc';

  ngOnInit(): void {
    this.loadCheckins();
    this.setupRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadCheckins(): void {
    this.loading = true;
    this.error = null;

    this.guestService.getTodayCheckins()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (checkins) => {
          this.checkins = checkins;
          this.calculateStats();
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading checkins:', error);
          this.error = 'Failed to load check-ins. Please try again.';
          this.loading = false;
        }
      });
  }

  private setupRealTimeUpdates(): void {
    // Listen for booking updates
    this.signalRService.bookingUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.refreshCheckinData();
      });
  }

  private refreshCheckinData(): void {
    this.guestService.getTodayCheckins()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (checkins) => {
          this.checkins = checkins;
          this.calculateStats();
          this.applyFilters();
        },
        error: (error) => {
          console.error('Error refreshing checkins:', error);
        }
      });
  }

  private calculateStats(): void {
    const total = this.checkins.length;
    const completed = this.checkins.filter(c => c.status === 'completed').length;
    const pending = this.checkins.filter(c => c.status === 'pending').length;
    const inProgress = this.checkins.filter(c => c.status === 'in-progress').length;
    const noShow = this.checkins.filter(c => c.status === 'no-show').length;

    this.stats = {
      total,
      completed,
      pending,
      inProgress,
      noShow,
      completionRate: total > 0 ? Math.round((completed / total) * 100) : 0
    };
  }

  applyFilters(): void {
    let filtered = [...this.checkins];

    // Text search
    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(checkin =>
        checkin.guestName.toLowerCase().includes(term) ||
        checkin.phoneNumber.includes(term) ||
        checkin.roomNumber.toLowerCase().includes(term) ||
        checkin.specialRequests?.toLowerCase().includes(term)
      );
    }

    // Status filter
    if (this.statusFilter !== 'all') {
      filtered = filtered.filter(checkin =>
        checkin.status === this.statusFilter
      );
    }

    // Time filter
    if (this.timeFilter !== 'all') {
      const now = new Date();
      filtered = filtered.filter(checkin => {
        switch (this.timeFilter) {
          case 'overdue':
            return checkin.status === 'pending' && checkin.expectedArrival < now;
          case 'upcoming':
            return checkin.expectedArrival > now;
          case 'today':
            return this.isSameDay(checkin.expectedArrival, now);
          default:
            return true;
        }
      });
    }

    // Apply sorting
    filtered = this.sortCheckins(filtered);

    this.filteredCheckins = filtered;
    this.totalItems = filtered.length;
  }

  private isSameDay(date1: Date | string, date2: Date | string): boolean {
    const d1 = typeof date1 === 'string' ? new Date(date1) : date1;
    const d2 = typeof date2 === 'string' ? new Date(date2) : date2;

    // Check if dates are valid
    if (!d1 || !d2 || isNaN(d1.getTime()) || isNaN(d2.getTime())) {
      return false;
    }

    return d1.toDateString() === d2.toDateString();
  }

  private sortCheckins(checkins: CheckinSummary[]): CheckinSummary[] {
    return checkins.sort((a, b) => {
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

  sort(field: keyof CheckinSummary): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.statusFilter = 'all';
    this.timeFilter = 'today';
    this.currentPage = 1;
    this.applyFilters();
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'completed': return 'badge bg-success';
      case 'in-progress': return 'badge bg-info';
      case 'pending': return 'badge bg-warning';
      case 'no-show': return 'badge bg-danger';
      default: return 'badge bg-secondary';
    }
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'completed': return 'check-circle';
      case 'in-progress': return 'clock';
      case 'pending': return 'circle';
      case 'no-show': return 'x-circle';
      default: return 'help-circle';
    }
  }

  isOverdue(checkin: CheckinSummary): boolean {
    const now = new Date();
    return checkin.status === 'pending' && checkin.expectedArrival < now;
  }

  getTimeStatus(expectedArrival: Date | string, actualArrival?: Date | string): string {
    // Convert string dates to Date objects
    const expectedDate = typeof expectedArrival === 'string' ? new Date(expectedArrival) : expectedArrival;
    const actualDate = actualArrival ? (typeof actualArrival === 'string' ? new Date(actualArrival) : actualArrival) : undefined;
    const now = new Date();

    // Check for valid dates
    if (!expectedDate || isNaN(expectedDate.getTime())) {
      return 'Unknown';
    }

    if (actualDate && !isNaN(actualDate.getTime())) {
      const diffMinutes = Math.floor((actualDate.getTime() - expectedDate.getTime()) / (1000 * 60));
      if (diffMinutes > 30) return 'Late';
      if (diffMinutes < -30) return 'Early';
      return 'On Time';
    }

    if (expectedDate < now) {
      const diffMinutes = Math.floor((now.getTime() - expectedDate.getTime()) / (1000 * 60));
      if (diffMinutes > 60) return `${Math.floor(diffMinutes / 60)}h overdue`;
      return `${diffMinutes}m overdue`;
    }

    const diffMinutes = Math.floor((expectedDate.getTime() - now.getTime()) / (1000 * 60));
    if (diffMinutes > 60) return `${Math.floor(diffMinutes / 60)}h remaining`;
    return `${diffMinutes}m remaining`;
  }

  updateStatus(checkinId: number, newStatus: string, event: Event): void {
    event.stopPropagation();

    this.guestService.updateCheckinStatus(checkinId, newStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshCheckinData();
        },
        error: (error) => {
          console.error('Error updating checkin status:', error);
        }
      });
  }

  viewGuestDetails(checkin: CheckinSummary): void {
    console.log('Viewing guest details:', checkin.guestName);
    // TODO: Navigate to guest detail view
  }

  sendWelcomeMessage(checkin: CheckinSummary, event: Event): void {
    event.stopPropagation();

    const welcomeMessage = `Welcome to our hotel, ${checkin.guestName}! Your room ${checkin.roomNumber} is ready. If you need assistance, please don't hesitate to contact us.`;

    this.guestService.sendMessage(checkin.phoneNumber, welcomeMessage)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log('Welcome message sent to:', checkin.phoneNumber);
        },
        error: (error) => {
          console.error('Error sending welcome message:', error);
        }
      });
  }

  get paginatedCheckins(): CheckinSummary[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    return this.filteredCheckins.slice(startIndex, endIndex);
  }

  get totalPages(): number {
    return Math.ceil(this.totalItems / this.pageSize);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  refresh(): void {
    this.loadCheckins();
  }

  // Make Math available to template
  Math = Math;
}