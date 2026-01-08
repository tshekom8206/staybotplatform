import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { Subject, takeUntil, forkJoin, timer } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  UserActivityService,
  UserActivity,
  ActivityFilter,
  ActivityStats,
  ActivityResponse
} from '../../../../core/services/user-activity.service';

@Component({
  selector: 'app-activity',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbAlertModule,
    FeatherIconDirective
  ],
  templateUrl: './activity.component.html',
  styleUrls: ['./activity.component.scss']
})
export class ActivityComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private userActivityService = inject(UserActivityService);
  private formBuilder = inject(FormBuilder);

  // Data properties
  activities: UserActivity[] = [];
  stats: ActivityStats | null = null;
  availableActions: string[] = [];
  availableEntities: string[] = [];

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Pagination and filtering
  currentPage = 1;
  totalPages = 1;
  totalCount = 0;
  pageSize = 20;
  searchTerm = '';
  selectedAction = '';
  selectedEntity = '';
  selectedUser = '';
  selectedDateRange = '';

  // Date range filter form
  dateRangeForm: FormGroup;

  // Tab management
  activeTab = 'all';

  constructor() {
    this.dateRangeForm = this.formBuilder.group({
      dateFrom: [''],
      dateTo: ['']
    });
  }

  ngOnInit() {
    this.loadData();
    this.setupAutoRefresh();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.loading = true;
    this.error = null;

    forkJoin({
      activities: this.userActivityService.getActivities(this.buildFilter()),
      stats: this.userActivityService.getActivityStats(),
      actions: this.userActivityService.getAvailableActions(),
      entities: this.userActivityService.getAvailableEntities()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (data) => {
        this.activities = data.activities.activities;
        this.totalCount = data.activities.totalCount;
        this.totalPages = data.activities.totalPages;
        this.currentPage = data.activities.currentPage;
        this.pageSize = data.activities.pageSize;
        this.stats = data.stats;
        this.availableActions = data.actions;
        this.availableEntities = data.entities;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading activity data:', error);
        this.error = 'Failed to load activity data. Please try again.';
        this.loading = false;
      }
    });
  }

  setupAutoRefresh() {
    // Auto-refresh every 30 seconds
    timer(30000, 30000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (!this.loading) {
          this.refreshData();
        }
      });
  }

  refreshData() {
    this.userActivityService.getActivities(this.buildFilter())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.activities = data.activities;
          this.totalCount = data.totalCount;
          this.totalPages = data.totalPages;
        },
        error: (error) => {
          console.error('Error refreshing activity data:', error);
        }
      });

    this.userActivityService.getActivityStats()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.stats = stats;
        },
        error: (error) => {
          console.error('Error refreshing activity stats:', error);
        }
      });
  }

  buildFilter(): ActivityFilter {
    const filter: ActivityFilter = {
      page: this.currentPage,
      pageSize: this.pageSize,
      sortBy: 'CreatedAt',
      sortDirection: 'desc'
    };

    if (this.searchTerm.trim()) {
      filter.searchTerm = this.searchTerm;
    }

    if (this.selectedAction) {
      filter.action = this.selectedAction;
    }

    if (this.selectedEntity) {
      filter.entity = this.selectedEntity;
    }

    // Apply date range
    const dateRangeValue = this.dateRangeForm.value;
    if (dateRangeValue.dateFrom) {
      filter.dateFrom = new Date(dateRangeValue.dateFrom);
    }
    if (dateRangeValue.dateTo) {
      filter.dateTo = new Date(dateRangeValue.dateTo);
    }

    // Apply predefined date range
    if (this.selectedDateRange) {
      const now = new Date();
      switch (this.selectedDateRange) {
        case 'today':
          filter.dateFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate());
          filter.dateTo = now;
          break;
        case 'week':
          filter.dateFrom = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
          filter.dateTo = now;
          break;
        case 'month':
          filter.dateFrom = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
          filter.dateTo = now;
          break;
        case 'quarter':
          filter.dateFrom = new Date(now.getFullYear(), now.getMonth() - 3, now.getDate());
          filter.dateTo = now;
          break;
      }
    }

    return filter;
  }

  onSearch() {
    this.currentPage = 1;
    this.loadData();
  }

  onFilterChange() {
    this.currentPage = 1;
    this.loadData();
  }

  onDateRangeChange() {
    this.selectedDateRange = ''; // Clear predefined range when custom range is used
    this.currentPage = 1;
    this.loadData();
  }

  onPredefinedDateRangeChange() {
    // Clear custom date range when predefined range is selected
    this.dateRangeForm.patchValue({
      dateFrom: '',
      dateTo: ''
    });
    this.currentPage = 1;
    this.loadData();
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
    this.applyTabFilter();
  }

  applyTabFilter() {
    this.currentPage = 1;

    switch (this.activeTab) {
      case 'today':
        this.selectedDateRange = 'today';
        break;
      case 'week':
        this.selectedDateRange = 'week';
        break;
      case 'month':
        this.selectedDateRange = 'month';
        break;
      case 'users':
        this.selectedEntity = 'User';
        break;
      case 'tasks':
        this.selectedEntity = 'Task';
        break;
      default:
        this.selectedDateRange = '';
        this.selectedEntity = '';
    }

    this.loadData();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedAction = '';
    this.selectedEntity = '';
    this.selectedUser = '';
    this.selectedDateRange = '';
    this.activeTab = 'all';
    this.currentPage = 1;
    this.dateRangeForm.reset();
    this.loadData();
  }

  onPageChange(page: number) {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.loadData();
    }
  }

  // Utility methods
  getActivityIcon(action: string): string {
    return this.userActivityService.getActivityIcon(action);
  }

  getActivityColor(action: string): string {
    return this.userActivityService.getActivityColor(action);
  }

  getActionIcon(action: string): string {
    const actionLower = action.toLowerCase();
    if (actionLower.includes('create') || actionLower.includes('add')) return 'plus-circle';
    if (actionLower.includes('update') || actionLower.includes('edit')) return 'edit';
    if (actionLower.includes('delete') || actionLower.includes('remove')) return 'trash-2';
    if (actionLower.includes('login') || actionLower.includes('auth')) return 'log-in';
    if (actionLower.includes('logout')) return 'log-out';
    if (actionLower.includes('view') || actionLower.includes('read')) return 'eye';
    if (actionLower.includes('assign')) return 'user-plus';
    if (actionLower.includes('complete') || actionLower.includes('resolve')) return 'check-circle';
    if (actionLower.includes('send') || actionLower.includes('message')) return 'send';
    return 'activity';
  }

  getActionIconBgClass(action: string): string {
    const actionLower = action.toLowerCase();
    if (actionLower.includes('create') || actionLower.includes('add')) return 'bg-success';
    if (actionLower.includes('update') || actionLower.includes('edit')) return 'bg-info';
    if (actionLower.includes('delete') || actionLower.includes('remove')) return 'bg-danger';
    if (actionLower.includes('login') || actionLower.includes('auth')) return 'bg-primary';
    if (actionLower.includes('logout')) return 'bg-secondary';
    if (actionLower.includes('view') || actionLower.includes('read')) return 'bg-light';
    if (actionLower.includes('assign')) return 'bg-warning';
    if (actionLower.includes('complete') || actionLower.includes('resolve')) return 'bg-success';
    if (actionLower.includes('send') || actionLower.includes('message')) return 'bg-info';
    return 'bg-secondary';
  }

  getActivityDescription(activity: UserActivity): string {
    return this.userActivityService.getActivityDescription(activity);
  }

  getEntityIcon(entity: string): string {
    return this.userActivityService.getEntityIcon(entity);
  }

  formatEntityName(entity: string): string {
    return this.userActivityService.formatEntityName(entity);
  }

  formatActionName(action: string): string {
    return this.userActivityService.formatActionName(action);
  }

  formatDate(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString('en-ZA', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      timeZone: 'Africa/Johannesburg'
    });
  }

  formatDateTime(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleString('en-ZA', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Africa/Johannesburg'
    });
  }

  getTimeAgo(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    const now = new Date();
    const diffInMs = now.getTime() - d.getTime();
    const diffInMinutes = Math.floor(diffInMs / (1000 * 60));
    const diffInHours = Math.floor(diffInMs / (1000 * 60 * 60));
    const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24));

    if (diffInMinutes < 1) {
      return 'Just now';
    } else if (diffInMinutes < 60) {
      return `${diffInMinutes}m ago`;
    } else if (diffInHours < 24) {
      return `${diffInHours}h ago`;
    } else if (diffInDays < 7) {
      return `${diffInDays}d ago`;
    } else {
      return this.formatDate(d);
    }
  }

  getPaginationNumbers(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;

    if (this.totalPages <= maxVisible) {
      for (let i = 1; i <= this.totalPages; i++) {
        pages.push(i);
      }
    } else {
      const half = Math.floor(maxVisible / 2);
      let start = Math.max(1, this.currentPage - half);
      let end = Math.min(this.totalPages, start + maxVisible - 1);

      if (end - start < maxVisible - 1) {
        start = Math.max(1, end - maxVisible + 1);
      }

      for (let i = start; i <= end; i++) {
        pages.push(i);
      }
    }

    return pages;
  }

  showSuccessMessage(message: string) {
    this.successMessage = message;
    setTimeout(() => {
      this.successMessage = null;
    }, 5000);
  }

  dismissError() {
    this.error = null;
  }

  dismissSuccess() {
    this.successMessage = null;
  }

  // Stats helper methods
  getTotalActivitiesCount(): number {
    return this.stats?.totalActivities || 0;
  }

  getTodayActivitiesCount(): number {
    return this.stats?.todayActivities || 0;
  }

  getWeekActivitiesCount(): number {
    return this.stats?.weekActivities || 0;
  }

  getActiveUsersCount(): number {
    return this.stats?.totalActiveUsers || 0;
  }

  getTopActionsByCount(): Array<{ action: string, count: number }> {
    if (!this.stats?.activitiesByAction) return [];

    return Object.entries(this.stats.activitiesByAction)
      .map(([action, count]) => ({ action, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5);
  }

  getTopEntitiesByCount(): Array<{ entity: string, count: number }> {
    if (!this.stats?.activitiesByEntity) return [];

    return Object.entries(this.stats.activitiesByEntity)
      .map(([entity, count]) => ({ entity, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5);
  }

  exportActivities() {
    const filter = this.buildFilter();
    filter.pageSize = 1000; // Export more records

    this.userActivityService.getActivities(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          const exportData = data.activities.map(activity => ({
            timestamp: activity.createdAt,
            user: activity.actorUserEmail,
            action: activity.action,
            entity: activity.entity,
            entityId: activity.entityId,
            description: this.getActivityDescription(activity),
            details: activity.details
          }));

          const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `activity-log-${new Date().toISOString().split('T')[0]}.json`;
          link.click();
          window.URL.revokeObjectURL(url);

          this.showSuccessMessage('Activity log exported successfully');
        },
        error: (error) => {
          console.error('Error exporting activities:', error);
          this.error = 'Failed to export activity log';
        }
      });
  }
}
