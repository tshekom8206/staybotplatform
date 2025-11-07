import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AnalyticsFilter {
  dateFrom?: Date;
  dateTo?: Date;
  period?: 'day' | 'week' | 'month' | 'year';
  metricType?: string;
  includeInactive?: boolean;
}

export interface DashboardSummary {
  totalBookings: number;
  activeConversations: number;
  completedTasks: number;
  pendingTasks: number;
  emergencyIncidents: number;
  occupancyRate: number;
  averageResponseTime: string; // TimeSpan as string
  totalGuestInteractions: number;
  tasksByStatus: { [key: string]: number };
  conversationsByDay: { [key: string]: number };
}

export interface UserEngagement {
  totalActiveUsers: number;
  usersThisWeek: number;
  usersThisMonth: number;
  averageSessionDuration: number;
  totalSessions: number;
  topActiveUsers: UserActivitySummary[];
  loginsByHour: { [key: string]: number };
  activityByDay: { [key: string]: number };
}

export interface UserActivitySummary {
  userId: number;
  email: string;
  role: string;
  totalActions: number;
  lastActive: Date;
  totalTimeActive?: string; // TimeSpan as string
}

export interface TaskAnalytics {
  totalTasks: number;
  completedTasks: number;
  pendingTasks: number;
  overdueTasks: number;
  completionRate: number;
  averageCompletionTime: string; // TimeSpan as string
  tasksByType: { [key: string]: number };
  tasksByPriority: { [key: string]: number };
  completionTimeByType: { [key: string]: number };
  staffPerformance: TaskPerformance[];
}

export interface TaskPerformance {
  staffId: number;
  staffEmail: string;
  role: string;
  assignedTasks: number;
  completedTasks: number;
  completionRate: number;
  averageCompletionTime: string; // TimeSpan as string
}

export interface ConversationAnalytics {
  totalConversations: number;
  activeConversations: number;
  resolvedConversations: number;
  averageMessagesPerConversation: number;
  averageResolutionTime: string; // TimeSpan as string
  conversationsByType: { [key: string]: number };
  messagesByHour: { [key: string]: number };
  popularRequests: PopularRequest[];
}

export interface PopularRequest {
  requestType: string;
  count: number;
  percentage: number;
  averageResponseTime: string; // TimeSpan as string
}

export interface PerformanceMetrics {
  systemUptime: number;
  averageResponseTime: string; // TimeSpan as string
  totalApiCalls: number;
  errorRate: number;
  responseTimesByEndpoint: { [key: string]: number };
  errorsByType: { [key: string]: number };
  performanceHistory: PerformanceTimeSeries[];
}

export interface PerformanceTimeSeries {
  timestamp: Date;
  responseTime: number;
  requestCount: number;
  errorCount: number;
}

export interface ChartDataPoint {
  label: string;
  value: number;
  color?: string;
}

export interface TimeSeriesDataPoint {
  timestamp: Date;
  value: number;
  label?: string;
}

// Business Impact Dashboard Interfaces
export interface SatisfactionRevenueCorrelation {
  satisfactionLevels: SatisfactionLevel[];
  averageLifetimeValue: number;
  revenuePerSatisfactionPoint: number;
  returnRateComparison: {
    satisfied: number;
    unsatisfied: number;
  };
  correlationStrength: number;
}

export interface SatisfactionLevel {
  level: number;
  count: number;
  averageLifetimeValue: number;
  returnRate: number;
}

export interface GuestSegment {
  id: string;
  name: string;
  valueTier: 'Low' | 'Medium' | 'High' | 'Premium';
  satisfactionLevel: 'Low' | 'Medium' | 'High' | 'Excellent';
  guestCount: number;
  revenueContribution: number;
  averageSpend: number;
  retentionRate: number;
  recommendedActions: string[];
}

export interface SurveyPerformance {
  totalSent: number;
  totalDelivered: number;
  totalOpened: number;
  totalCompleted: number;
  conversionRates: {
    deliveryRate: number;
    openRate: number;
    completionRate: number;
  };
  dailyTrends: DailySurveyTrend[];
  benchmarks: {
    industryDeliveryRate: number;
    industryOpenRate: number;
    industryCompletionRate: number;
  };
}

export interface DailySurveyTrend {
  date: Date;
  sent: number;
  delivered: number;
  opened: number;
  completed: number;
}

export interface BusinessImpact {
  currentSatisfactionScore: number;
  potentialRevenueIncrease: number;
  roiProjection: number;
  satisfactionTarget: number;
  timeToTarget: number; // months
  recommendedActions: RecommendedAction[];
  keyMetrics: {
    totalLifetimeValueTracked: number;
    averageSatisfactionScore: number;
    surveyResponseRate: number;
    revenueOpportunity: number;
  };
}

export interface RecommendedAction {
  category: string;
  action: string;
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  estimatedImpact: number;
  timeframe: string;
  resources: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/analytics`;

  /**
   * Get dashboard summary statistics
   */
  getDashboardSummary(filter?: AnalyticsFilter): Observable<DashboardSummary> {
    const params = this.buildParams(filter);
    return this.http.get<DashboardSummary>(`${this.apiUrl}/dashboard`, { params });
  }

  /**
   * Get user engagement analytics
   */
  getUserEngagement(filter?: AnalyticsFilter): Observable<UserEngagement> {
    const params = this.buildParams(filter);
    return this.http.get<UserEngagement>(`${this.apiUrl}/users`, { params });
  }

  /**
   * Get task analytics
   */
  getTaskAnalytics(filter?: AnalyticsFilter): Observable<TaskAnalytics> {
    const params = this.buildParams(filter);
    return this.http.get<TaskAnalytics>(`${this.apiUrl}/tasks`, { params });
  }

  /**
   * Get conversation analytics
   */
  getConversationAnalytics(filter?: AnalyticsFilter): Observable<ConversationAnalytics> {
    const params = this.buildParams(filter);
    return this.http.get<ConversationAnalytics>(`${this.apiUrl}/conversations`, { params });
  }

  /**
   * Get performance metrics
   */
  getPerformanceMetrics(filter?: AnalyticsFilter): Observable<PerformanceMetrics> {
    const params = this.buildParams(filter);
    return this.http.get<PerformanceMetrics>(`${this.apiUrl}/performance`, { params });
  }

  /**
   * Business Impact Dashboard Methods - NEW API Endpoints
   */

  /**
   * Get hotel performance metrics (occupancy, ADR, RevPAR, GSS, NPS)
   */
  getHotelPerformance(): Observable<any> {
    return this.http.get(`${this.apiUrl}/hotel-performance`);
  }

  /**
   * Get operational performance by department
   */
  getOperationalPerformance(): Observable<any> {
    return this.http.get(`${this.apiUrl}/operational-performance`);
  }

  /**
   * Get guest satisfaction trends with critical alerts
   */
  getGuestSatisfactionTrends(): Observable<any> {
    return this.http.get(`${this.apiUrl}/guest-satisfaction-trends`);
  }

  /**
   * Get revenue insights and satisfaction correlation
   */
  getRevenueInsights(): Observable<any> {
    return this.http.get(`${this.apiUrl}/revenue-insights`);
  }

  getUpsellingRoi(): Observable<any> {
    return this.http.get(`${this.apiUrl}/upselling-roi`);
  }

  getImmediateActions(): Observable<any> {
    return this.http.get(`${environment.apiUrl}/dashboard/immediate-actions`);
  }

  /**
   * Export Business Impact Report as PDF
   */
  exportBusinessImpactReport(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export-business-impact-report`, {
      responseType: 'blob'
    });
  }

  /**
   * OLD METHODS - Deprecated, kept for backward compatibility
   */
  getSatisfactionRevenueCorrelation(filter?: AnalyticsFilter): Observable<SatisfactionRevenueCorrelation> {
    const params = this.buildParams(filter);
    return this.http.get<SatisfactionRevenueCorrelation>(`${this.apiUrl}/satisfaction-revenue-correlation`, { params });
  }

  getGuestSegments(filter?: AnalyticsFilter): Observable<GuestSegment[]> {
    const params = this.buildParams(filter);
    return this.http.get<GuestSegment[]>(`${this.apiUrl}/guest-segments`, { params });
  }

  getSurveyPerformance(filter?: AnalyticsFilter): Observable<SurveyPerformance> {
    const params = this.buildParams(filter);
    return this.http.get<SurveyPerformance>(`${this.apiUrl}/survey-performance`, { params });
  }

  getBusinessImpact(filter?: AnalyticsFilter): Observable<BusinessImpact> {
    const params = this.buildParams(filter);
    return this.http.get<BusinessImpact>(`${this.apiUrl}/revenue-impact`, { params });
  }

  /**
   * Build HTTP params from filter
   */
  private buildParams(filter?: AnalyticsFilter): HttpParams {
    let params = new HttpParams();

    if (filter) {
      if (filter.dateFrom) {
        params = params.set('dateFrom', filter.dateFrom.toISOString());
      }
      if (filter.dateTo) {
        params = params.set('dateTo', filter.dateTo.toISOString());
      }
      if (filter.period) {
        params = params.set('period', filter.period);
      }
      if (filter.metricType) {
        params = params.set('metricType', filter.metricType);
      }
      if (filter.includeInactive !== undefined) {
        params = params.set('includeInactive', filter.includeInactive.toString());
      }
    }

    return params;
  }

  /**
   * Create date range filter
   */
  createDateRangeFilter(days: number): AnalyticsFilter {
    const now = new Date();
    const dateFrom = new Date(now);
    dateFrom.setDate(dateFrom.getDate() - days);

    return {
      dateFrom,
      dateTo: now
    };
  }

  /**
   * Create period filter
   */
  createPeriodFilter(period: 'day' | 'week' | 'month' | 'year'): AnalyticsFilter {
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

    return {
      dateFrom,
      dateTo: now,
      period
    };
  }

  /**
   * Convert dictionary to chart data points
   */
  dictionaryToChartData(dict: { [key: string]: number }, colors?: string[]): ChartDataPoint[] {
    return Object.entries(dict).map(([key, value], index) => ({
      label: key,
      value,
      color: colors?.[index] || this.getDefaultColor(index)
    }));
  }

  /**
   * Convert time series data for charting
   */
  prepareTimeSeriesData(dict: { [key: string]: number }, dateFormat: 'day' | 'hour' = 'day'): TimeSeriesDataPoint[] {
    return Object.entries(dict).map(([key, value]) => {
      let timestamp: Date;

      if (dateFormat === 'day') {
        timestamp = new Date(key);
      } else {
        // For hour format (assuming key is hour string like "14")
        const hour = parseInt(key);
        timestamp = new Date();
        timestamp.setHours(hour, 0, 0, 0);
      }

      return {
        timestamp,
        value,
        label: key
      };
    }).sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
  }

  /**
   * Get default color for chart index
   */
  getDefaultColor(index: number): string {
    const colors = [
      '#25D466', // Primary green
      '#17a2b8', // Info blue
      '#ffc107', // Warning yellow
      '#dc3545', // Danger red
      '#6f42c1', // Purple
      '#fd7e14', // Orange
      '#20c997', // Teal
      '#6610f2', // Indigo
      '#e83e8c', // Pink
      '#6c757d'  // Gray
    ];

    return colors[index % colors.length];
  }

  /**
   * Format percentage
   */
  formatPercentage(value: number | null | undefined, decimals: number = 1): string {
    if (value === null || value === undefined || isNaN(Number(value))) {
      return '0.0%';
    }
    return `${Number(value).toFixed(decimals)}%`;
  }

  /**
   * Format number with commas
   */
  formatNumber(value: number | null | undefined): string {
    if (value === null || value === undefined || isNaN(Number(value))) {
      return '0';
    }
    return Number(value).toLocaleString();
  }

  /**
   * Format time span string to readable format
   */
  formatTimeSpan(timeSpanString: string): string {
    if (!timeSpanString) return '0m';

    // Parse .NET TimeSpan format (e.g., "00:15:30" or "1.02:30:45")
    const parts = timeSpanString.split(':');
    if (parts.length < 2) return timeSpanString;

    const lastPart = parts[parts.length - 1];
    const minutes = parseInt(parts[parts.length - 2]);
    const hours = parts.length > 2 ? parseInt(parts[parts.length - 3]) : 0;

    // Handle days (format: "1.02:30:45")
    let days = 0;
    if (parts.length > 2 && parts[0].includes('.')) {
      const dayPart = parts[0].split('.');
      days = parseInt(dayPart[0]);
    }

    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    } else if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else if (minutes > 0) {
      return `${minutes}m`;
    } else {
      return '< 1m';
    }
  }

  /**
   * Get metric icon based on type
   */
  getMetricIcon(metricType: string): string {
    switch (metricType.toLowerCase()) {
      case 'bookings':
        return 'calendar';
      case 'conversations':
        return 'message-square';
      case 'tasks':
        return 'clipboard';
      case 'users':
        return 'users';
      case 'occupancy':
        return 'home';
      case 'response':
      case 'responsetime':
        return 'clock';
      case 'emergency':
        return 'alert-triangle';
      case 'performance':
        return 'trending-up';
      case 'errors':
        return 'alert-circle';
      default:
        return 'bar-chart';
    }
  }

  /**
   * Get metric color class based on type
   */
  getMetricColorClass(metricType: string): string {
    switch (metricType.toLowerCase()) {
      case 'bookings':
        return 'text-primary';
      case 'conversations':
        return 'text-info';
      case 'tasks':
        return 'text-warning';
      case 'users':
        return 'text-success';
      case 'occupancy':
        return 'text-secondary';
      case 'emergency':
        return 'text-danger';
      case 'performance':
        return 'text-success';
      case 'errors':
        return 'text-danger';
      default:
        return 'text-muted';
    }
  }

  /**
   * Calculate growth percentage
   */
  calculateGrowth(current: number, previous: number): number {
    if (previous === 0) return current > 0 ? 100 : 0;
    return ((current - previous) / previous) * 100;
  }

  /**
   * Format growth as string with sign
   */
  formatGrowth(growth: number): string {
    const sign = growth >= 0 ? '+' : '';
    return `${sign}${growth.toFixed(1)}%`;
  }

  /**
   * Get growth color class
   */
  getGrowthColorClass(growth: number): string {
    return growth >= 0 ? 'text-success' : 'text-danger';
  }

  /**
   * Get growth icon
   */
  getGrowthIcon(growth: number): string {
    return growth >= 0 ? 'trending-up' : 'trending-down';
  }

  /**
   * Get task priority color
   */
  getTaskPriorityColor(priority: string): string {
    switch (priority.toLowerCase()) {
      case 'low':
        return '#28a745'; // Green
      case 'medium':
        return '#ffc107'; // Yellow
      case 'high':
        return '#fd7e14'; // Orange
      case 'urgent':
        return '#dc3545'; // Red
      default:
        return '#6c757d'; // Gray
    }
  }

  /**
   * Get task status color
   */
  getTaskStatusColor(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed':
        return '#28a745'; // Green
      case 'in progress':
      case 'inprogress':
        return '#17a2b8'; // Blue
      case 'pending':
        return '#ffc107'; // Yellow
      case 'overdue':
        return '#dc3545'; // Red
      default:
        return '#6c757d'; // Gray
    }
  }

  /**
   * Aggregate data by time period
   */
  aggregateByPeriod(data: TimeSeriesDataPoint[], period: 'hour' | 'day' | 'week' | 'month'): TimeSeriesDataPoint[] {
    const grouped = new Map<string, { sum: number; count: number; timestamp: Date }>();

    data.forEach(point => {
      let key: string;
      let periodStart: Date;

      switch (period) {
        case 'hour':
          periodStart = new Date(point.timestamp);
          periodStart.setMinutes(0, 0, 0);
          key = periodStart.toISOString();
          break;
        case 'day':
          periodStart = new Date(point.timestamp);
          periodStart.setHours(0, 0, 0, 0);
          key = periodStart.toISOString();
          break;
        case 'week':
          periodStart = new Date(point.timestamp);
          const dayOfWeek = periodStart.getDay();
          periodStart.setDate(periodStart.getDate() - dayOfWeek);
          periodStart.setHours(0, 0, 0, 0);
          key = periodStart.toISOString();
          break;
        case 'month':
          periodStart = new Date(point.timestamp);
          periodStart.setDate(1);
          periodStart.setHours(0, 0, 0, 0);
          key = periodStart.toISOString();
          break;
      }

      if (!grouped.has(key)) {
        grouped.set(key, { sum: 0, count: 0, timestamp: periodStart });
      }

      const group = grouped.get(key)!;
      group.sum += point.value;
      group.count += 1;
    });

    return Array.from(grouped.values())
      .map(group => ({
        timestamp: group.timestamp,
        value: group.sum / group.count, // Average
        label: this.formatPeriodLabel(group.timestamp, period)
      }))
      .sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
  }

  /**
   * Format period label for display
   */
  private formatPeriodLabel(date: Date, period: 'hour' | 'day' | 'week' | 'month'): string {
    switch (period) {
      case 'hour':
        return date.toLocaleTimeString('en-ZA', { hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Johannesburg' });
      case 'day':
        return date.toLocaleDateString('en-ZA', { month: 'short', day: 'numeric', timeZone: 'Africa/Johannesburg' });
      case 'week':
        const endOfWeek = new Date(date);
        endOfWeek.setDate(endOfWeek.getDate() + 6);
        return `${date.toLocaleDateString('en-ZA', { month: 'short', day: 'numeric', timeZone: 'Africa/Johannesburg' })} - ${endOfWeek.toLocaleDateString('en-ZA', { month: 'short', day: 'numeric', timeZone: 'Africa/Johannesburg' })}`;
      case 'month':
        return date.toLocaleDateString('en-ZA', { month: 'long', year: 'numeric', timeZone: 'Africa/Johannesburg' });
      default:
        return date.toLocaleDateString('en-ZA', { timeZone: 'Africa/Johannesburg' });
    }
  }
}
