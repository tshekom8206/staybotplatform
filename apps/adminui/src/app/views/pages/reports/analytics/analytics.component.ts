import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { Subject, takeUntil, forkJoin, timer } from 'rxjs';
import { NgbDropdownModule, NgbTooltipModule, NgbAlertModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import {
  AnalyticsService,
  DashboardSummary,
  UserEngagement,
  TaskAnalytics,
  ConversationAnalytics,
  PerformanceMetrics,
  AnalyticsFilter,
  ChartDataPoint,
  TimeSeriesDataPoint
} from '../../../../core/services/analytics.service';

@Component({
  selector: 'app-analytics',
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
  templateUrl: './analytics.component.html',
  styleUrls: ['./analytics.component.scss']
})
export class AnalyticsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private analyticsService = inject(AnalyticsService);
  private formBuilder = inject(FormBuilder);

  // Data properties
  dashboardSummary: DashboardSummary | null = null;
  userEngagement: UserEngagement | null = null;
  taskAnalytics: TaskAnalytics | null = null;
  conversationAnalytics: ConversationAnalytics | null = null;
  performanceMetrics: PerformanceMetrics | null = null;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;
  activeTab: string = 'overview';

  // Filter form
  filterForm: FormGroup;
  selectedPeriod = 'week';

  // Chart data
  tasksByStatusChartData: ChartDataPoint[] = [];
  conversationsByDayChartData: TimeSeriesDataPoint[] = [];
  tasksByTypeChartData: ChartDataPoint[] = [];
  userActivityChartData: TimeSeriesDataPoint[] = [];

  // Tab management

  constructor() {
    this.filterForm = this.formBuilder.group({
      dateFrom: [''],
      dateTo: [''],
      period: ['week'],
      metricType: [''],
      includeInactive: [false]
    });
  }

  ngOnInit() {
    this.setupDefaultDateRange();
    this.loadData();
    this.setupAutoRefresh();
    this.setupFormSubscription();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setupDefaultDateRange() {
    const now = new Date();
    const weekAgo = new Date(now);
    weekAgo.setDate(weekAgo.getDate() - 7);

    this.filterForm.patchValue({
      dateFrom: weekAgo.toISOString().split('T')[0],
      dateTo: now.toISOString().split('T')[0],
      period: 'week'
    });
  }

  setupFormSubscription() {
    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (!this.loading) {
          this.loadData();
        }
      });
  }

  setupAutoRefresh() {
    // Auto-refresh every 5 minutes
    timer(300000, 300000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (!this.loading) {
          this.refreshData();
        }
      });
  }

  loadData() {
    this.loading = true;
    this.error = null;

    const filter = this.buildFilter();

    forkJoin({
      dashboard: this.analyticsService.getDashboardSummary(filter),
      userEngagement: this.analyticsService.getUserEngagement(filter),
      taskAnalytics: this.analyticsService.getTaskAnalytics(filter),
      conversationAnalytics: this.analyticsService.getConversationAnalytics(filter),
      performanceMetrics: this.analyticsService.getPerformanceMetrics(filter)
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (data) => {
        this.dashboardSummary = data.dashboard;
        this.userEngagement = data.userEngagement;
        this.taskAnalytics = data.taskAnalytics;
        this.conversationAnalytics = data.conversationAnalytics;
        this.performanceMetrics = data.performanceMetrics;

        this.prepareChartData();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading analytics data:', error);
        this.error = 'Failed to load analytics data. Please try again.';
        this.loading = false;
      }
    });
  }

  refreshData() {
    const filter = this.buildFilter();

    this.analyticsService.getDashboardSummary(filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.dashboardSummary = data;
          this.prepareChartData();
        },
        error: (error) => {
          console.error('Error refreshing dashboard data:', error);
        }
      });
  }

  buildFilter(): AnalyticsFilter {
    const formValue = this.filterForm.value;
    const filter: AnalyticsFilter = {};

    if (formValue.dateFrom) {
      filter.dateFrom = new Date(formValue.dateFrom);
    }
    if (formValue.dateTo) {
      filter.dateTo = new Date(formValue.dateTo);
    }
    if (formValue.period) {
      filter.period = formValue.period;
    }
    if (formValue.metricType) {
      filter.metricType = formValue.metricType;
    }
    filter.includeInactive = formValue.includeInactive || false;

    return filter;
  }

  prepareChartData() {
    // Prepare tasks by status chart data
    if (this.dashboardSummary?.tasksByStatus) {
      this.tasksByStatusChartData = this.analyticsService.dictionaryToChartData(
        this.dashboardSummary.tasksByStatus
      );
    }

    // Prepare conversations by day chart data
    if (this.dashboardSummary?.conversationsByDay) {
      this.conversationsByDayChartData = this.analyticsService.prepareTimeSeriesData(
        this.dashboardSummary.conversationsByDay,
        'day'
      );
    }

    // Prepare tasks by type chart data
    if (this.taskAnalytics?.tasksByType) {
      this.tasksByTypeChartData = this.analyticsService.dictionaryToChartData(
        this.taskAnalytics.tasksByType
      );
    }

    // Prepare user activity chart data
    if (this.userEngagement?.activityByDay) {
      this.userActivityChartData = this.analyticsService.prepareTimeSeriesData(
        this.userEngagement.activityByDay,
        'day'
      );
    }
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
  }

  setPeriod(period: string) {
    this.selectedPeriod = period;
    this.filterForm.patchValue({ period });

    // Update date range based on period
    const now = new Date();
    let dateFrom = new Date(now);

    switch (period) {
      case 'day':
        dateFrom.setDate(dateFrom.getDate() - 1);
        break;
      case 'week':
        dateFrom.setDate(dateFrom.getDate() - 7);
        break;
      case 'month':
        dateFrom.setMonth(dateFrom.getMonth() - 1);
        break;
      case 'year':
        dateFrom.setFullYear(dateFrom.getFullYear() - 1);
        break;
    }

    this.filterForm.patchValue({
      dateFrom: dateFrom.toISOString().split('T')[0],
      dateTo: now.toISOString().split('T')[0]
    });
  }

  // Utility methods
  formatNumber(value: number | undefined): string {
    return this.analyticsService.formatNumber(value || 0);
  }

  formatPercentage(value: number | undefined): string {
    return this.analyticsService.formatPercentage(value || 0);
  }

  formatTimeSpan(timeSpan: string | undefined): string {
    return this.analyticsService.formatTimeSpan(timeSpan || '');
  }

  getMetricIcon(metricType: string): string {
    return this.analyticsService.getMetricIcon(metricType);
  }

  getMetricColorClass(metricType: string): string {
    return this.analyticsService.getMetricColorClass(metricType);
  }

  getTaskStatusColor(status: string): string {
    return this.analyticsService.getTaskStatusColor(status);
  }

  getTaskPriorityColor(priority: string): string {
    return this.analyticsService.getTaskPriorityColor(priority);
  }

  calculateGrowth(current: number, previous: number): number {
    return this.analyticsService.calculateGrowth(current, previous);
  }

  formatGrowth(growth: number): string {
    return this.analyticsService.formatGrowth(growth);
  }

  getGrowthColorClass(growth: number): string {
    return this.analyticsService.getGrowthColorClass(growth);
  }

  getGrowthIcon(growth: number): string {
    return this.analyticsService.getGrowthIcon(growth);
  }

  // Data accessors with fallbacks
  getTotalBookings(): number {
    return this.dashboardSummary?.totalBookings || 0;
  }

  getActiveConversations(): number {
    return this.dashboardSummary?.activeConversations || 0;
  }

  getCompletedTasks(): number {
    return this.dashboardSummary?.completedTasks || 0;
  }

  getPendingTasks(): number {
    return this.dashboardSummary?.pendingTasks || 0;
  }

  getEmergencyIncidents(): number {
    return this.dashboardSummary?.emergencyIncidents || 0;
  }

  getOccupancyRate(): number {
    return this.dashboardSummary?.occupancyRate || 0;
  }

  getAverageResponseTime(): string {
    return this.formatTimeSpan(this.dashboardSummary?.averageResponseTime);
  }

  getTotalGuestInteractions(): number {
    return this.dashboardSummary?.totalGuestInteractions || 0;
  }

  getTotalActiveUsers(): number {
    return this.userEngagement?.totalActiveUsers || 0;
  }

  getUsersThisWeek(): number {
    return this.userEngagement?.usersThisWeek || 0;
  }

  getUsersThisMonth(): number {
    return this.userEngagement?.usersThisMonth || 0;
  }

  getTaskCompletionRate(): number {
    return this.taskAnalytics?.completionRate || 0;
  }

  getOverdueTasks(): number {
    return this.taskAnalytics?.overdueTasks || 0;
  }

  getAverageCompletionTime(): string {
    return this.formatTimeSpan(this.taskAnalytics?.averageCompletionTime);
  }

  getTotalConversations(): number {
    return this.conversationAnalytics?.totalConversations || 0;
  }

  getResolvedConversations(): number {
    return this.conversationAnalytics?.resolvedConversations || 0;
  }

  getAverageMessagesPerConversation(): number {
    return this.conversationAnalytics?.averageMessagesPerConversation || 0;
  }

  getAverageResolutionTime(): string {
    return this.formatTimeSpan(this.conversationAnalytics?.averageResolutionTime);
  }

  getSystemUptime(): number {
    return this.performanceMetrics?.systemUptime || 0;
  }

  getTotalApiCalls(): number {
    return this.performanceMetrics?.totalApiCalls || 0;
  }

  getErrorRate(): number {
    return this.performanceMetrics?.errorRate || 0;
  }

  getPerformanceResponseTime(): string {
    return this.formatTimeSpan(this.performanceMetrics?.averageResponseTime);
  }

  // Chart data helpers

  getTasksByTypeData(): Array<{label: string, value: number}> {
    if (!this.taskAnalytics?.tasksByType) return [];

    return Object.entries(this.taskAnalytics.tasksByType)
      .map(([type, count]) => ({ label: type, value: count }))
      .sort((a, b) => b.value - a.value);
  }

  getTopActiveUsers(): Array<{email: string, role: string, actions: number}> {
    if (!this.userEngagement?.topActiveUsers) return [];

    return this.userEngagement.topActiveUsers
      .slice(0, 5)
      .map(user => ({
        email: user.email,
        role: user.role,
        actions: user.totalActions
      }));
  }

  getPopularRequests(): Array<{type: string, count: number, percentage: number}> {
    if (!this.conversationAnalytics?.popularRequests) return [];

    return this.conversationAnalytics.popularRequests
      .slice(0, 5)
      .map(request => ({
        type: request.requestType,
        count: request.count,
        percentage: request.percentage
      }));
  }

  getStaffPerformance(): Array<{email: string, role: string, completionRate: number, completedCount: number, avgTime: string}> {
    if (!this.userEngagement?.topActiveUsers) return [];

    return this.userEngagement.topActiveUsers
      .slice(0, 5)
      .map((staff: any) => ({
        email: staff.email || 'Unknown',
        role: staff.role || 'Staff',
        completionRate: (staff.totalActions || 0) * 10, // Convert to percentage
        completedCount: staff.totalActions || 0,
        avgTime: '00:00:00' // Default since not available in current API
      }));
  }

  exportAnalytics() {
    const exportData = {
      reportDate: new Date().toISOString(),
      period: this.selectedPeriod,
      filter: this.buildFilter(),
      dashboard: this.dashboardSummary,
      userEngagement: this.userEngagement,
      taskAnalytics: this.taskAnalytics,
      conversationAnalytics: this.conversationAnalytics,
      performanceMetrics: this.performanceMetrics
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `analytics-report-${new Date().toISOString().split('T')[0]}.json`;
    link.click();
    window.URL.revokeObjectURL(url);

    this.showSuccessMessage('Analytics report exported successfully');
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

  // Additional methods required by the template
  get hasData(): boolean {
    return true; // Always show data for now
  }

  getResponseTimeColor(): string {
    const responseTime = this.parseTimeSpanToMs(this.dashboardSummary?.averageResponseTime) || 0;
    if (responseTime < 1000) return '#28a745'; // green
    if (responseTime < 3000) return '#ffc107'; // yellow
    return '#dc3545'; // red
  }

  getResponseTimePercentage(): number {
    const responseTime = this.parseTimeSpanToMs(this.dashboardSummary?.averageResponseTime) || 0;
    // Convert response time to percentage (inverse - lower is better)
    const maxTime = 5000; // 5 seconds as max
    return Math.max(0, Math.min(100, 100 - (responseTime / maxTime) * 100));
  }

  getRequestCategoryData(): Array<{label: string, value: number, color: string}> {
    const colors = ['#007bff', '#28a745', '#ffc107', '#dc3545', '#6f42c1', '#fd7e14'];

    if (!this.conversationAnalytics?.popularRequests) return [];

    return this.conversationAnalytics.popularRequests
      .slice(0, 6)
      .map((request, index) => ({
        label: request.requestType,
        value: request.count,
        color: colors[index % colors.length]
      }));
  }

  getDailyActivityData(): Array<{label: string, value: number}> {
    const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    const mockData = [45, 52, 38, 67, 41, 33, 29]; // Mock daily activity data

    return days.map((day, index) => ({
      label: day,
      value: mockData[index]
    }));
  }

  trackByLabel(index: number, item: any): string {
    return item.label;
  }

  getMaxValue(data: Array<{value: number}>): number {
    return data.length > 0 ? Math.max(...data.map(d => d.value)) : 1;
  }

  getTasksByStatusData(): Array<{label: string, value: number, color: string}> {
    if (!this.dashboardSummary?.tasksByStatus) return [];

    const statusColors: {[key: string]: string} = {
      'Completed': '#28a745',
      'Pending': '#ffc107',
      'InProgress': '#17a2b8',
      'Cancelled': '#dc3545',
      'OnHold': '#6c757d'
    };

    return Object.entries(this.dashboardSummary.tasksByStatus)
      .map(([status, count]) => ({
        label: status,
        value: count as number,
        color: statusColors[status] || '#6c757d'
      }))
      .sort((a, b) => (b.value as number) - (a.value as number));
  }

  getTaskTypeColor(taskType: string): string {
    const typeColors: {[key: string]: string} = {
      'deliver_item': '#28a745',        // Green - delivery
      'collect_item': '#17a2b8',        // Cyan - collection
      'maintenance': '#dc3545',         // Red - maintenance
      'frontdesk': '#007bff',           // Blue - front desk
      'concierge': '#6f42c1',           // Purple - concierge
      'general': '#6c757d'              // Gray - general
    };

    return typeColors[taskType] || '#6c757d';
  }

  parseTimeSpanToMs(timeSpan?: string): number {
    if (!timeSpan) return 0;

    // Parse .NET TimeSpan format (e.g., "00:15:30" or "1.02:30:45.123")
    const parts = timeSpan.split(':');
    if (parts.length < 2) return 0;

    let hours = 0;
    let minutes = 0;
    let seconds = 0;
    let milliseconds = 0;

    // Handle seconds and possible milliseconds
    const secondsPart = parts[parts.length - 1];
    if (secondsPart.includes('.')) {
      const [secs, ms] = secondsPart.split('.');
      seconds = parseInt(secs) || 0;
      milliseconds = parseInt(ms.padEnd(3, '0').substring(0, 3)) || 0;
    } else {
      seconds = parseInt(secondsPart) || 0;
    }

    minutes = parseInt(parts[parts.length - 2]) || 0;

    if (parts.length >= 3) {
      const hoursPart = parts[parts.length - 3];
      // Handle days (format: "1.02:30:45")
      if (hoursPart.includes('.')) {
        const [days, hrs] = hoursPart.split('.');
        hours = parseInt(hrs) || 0;
        hours += (parseInt(days) || 0) * 24;
      } else {
        hours = parseInt(hoursPart) || 0;
      }
    }

    return (hours * 3600 + minutes * 60 + seconds) * 1000 + milliseconds;
  }
}