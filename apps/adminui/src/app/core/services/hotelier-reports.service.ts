import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Service Demand Heatmap Interfaces
export interface ServiceDemandHeatmapDto {
  data: ServiceDemandCell[];
  hours: string[];
  days: string[];
  totalByDepartment: { [key: string]: number };
  peakHour: number;
  peakDay: string;
}

export interface ServiceDemandCell {
  hour: number;
  dayOfWeek: number;
  dayName: string;
  count: number;
  department: string;
}

// Maintenance Trends Interfaces
export interface MaintenanceTrendsDto {
  byCategory: MaintenanceCategoryTrend[];
  topRooms: MaintenanceRoomTrend[];
  byFloor: MaintenanceFloorTrend[];
  repeatIssues: RepeatIssue[];
  totalIssues: number;
  avgResolutionHours: number;
  repeatRate: number;
}

export interface MaintenanceCategoryTrend {
  category: string;
  count: number;
  previousPeriodCount: number;
  changePercent: number;
  avgResolutionHours: number;
}

export interface MaintenanceRoomTrend {
  roomNumber: string;
  issueCount: number;
  topIssue: string;
  repeatCount: number;
}

export interface MaintenanceFloorTrend {
  floor: string;
  issueCount: number;
  percentOfTotal: number;
}

export interface RepeatIssue {
  roomNumber: string;
  category: string;
  occurrenceCount: number;
  firstReported: Date;
  lastReported: Date;
  status: string;
}

// Guest Journey Funnel Interfaces
export interface GuestJourneyFunnelDto {
  stages: FunnelStage[];
  overallConversionRate: number;
  biggestDropOff: string;
  biggestDropOffPercent: number;
}

export interface FunnelStage {
  name: string;
  description: string;
  count: number;
  conversionRate: number;
  dropOffRate: number;
  percentOfTotal: number;
}

// Response vs Satisfaction Correlation Interfaces
export interface ResponseSatisfactionCorrelationDto {
  dataPoints: ResponseSatisfactionPoint[];
  correlationStrength: number;
  correlationDescription: string;
  avgRatingUnder10Min: number;
  avgRatingOver30Min: number;
  insight: string;
}

export interface ResponseSatisfactionPoint {
  responseTimeBucket: string;
  minMinutes: number;
  maxMinutes: number;
  avgRating: number;
  sampleSize: number;
  percentOfTotal: number;
}

// WhatsApp Escalation Interfaces
export interface WhatsAppEscalationDto {
  totalConversations: number;
  botResolved: number;
  escalatedToAgent: number;
  escalationRate: number;
  botSuccessRate: number;
  byIntent: EscalationByIntent[];
  dailyTrend: EscalationTrend[];
  topEscalationReason: string;
}

export interface EscalationByIntent {
  intent: string;
  totalCount: number;
  escalatedCount: number;
  escalationRate: number;
}

export interface EscalationTrend {
  date: string;
  total: number;
  escalated: number;
  rate: number;
}

// Upsell Performance Interfaces
export interface UpsellPerformanceDto {
  totalSuggestions: number;
  totalAcceptances: number;
  conversionRate: number;
  totalRevenue: number;
  avgOrderValue: number;
  byCategory: UpsellByCategory[];
  topServices: UpsellByService[];
  weeklyTrend: UpsellTrend[];
}

export interface UpsellByCategory {
  category: string;
  suggestions: number;
  acceptances: number;
  conversionRate: number;
  revenue: number;
}

export interface UpsellByService {
  serviceName: string;
  price: number;
  suggestions: number;
  acceptances: number;
  conversionRate: number;
  revenue: number;
}

export interface UpsellTrend {
  week: string;
  suggestions: number;
  acceptances: number;
  conversionRate: number;
  revenue: number;
}

// Date range filter type
export type DateRangePreset = '7days' | '30days' | '90days' | 'custom';

@Injectable({
  providedIn: 'root'
})
export class HotelierReportsService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/analytics/hotelier`;

  /**
   * Get service demand heatmap showing request volume by hour and day
   */
  getServiceDemandHeatmap(startDate?: string, endDate?: string, department?: string): Observable<ServiceDemandHeatmapDto> {
    let params = this.buildDateParams(startDate, endDate);
    if (department) {
      params = params.set('department', department);
    }
    return this.http.get<ServiceDemandHeatmapDto>(`${this.apiUrl}/service-demand-heatmap`, { params });
  }

  /**
   * Get maintenance issue trends with category breakdown
   */
  getMaintenanceTrends(startDate?: string, endDate?: string): Observable<MaintenanceTrendsDto> {
    const params = this.buildDateParams(startDate, endDate);
    return this.http.get<MaintenanceTrendsDto>(`${this.apiUrl}/maintenance-trends`, { params });
  }

  /**
   * Get guest journey funnel from portal landing to action completion
   */
  getGuestJourneyFunnel(startDate?: string, endDate?: string): Observable<GuestJourneyFunnelDto> {
    const params = this.buildDateParams(startDate, endDate);
    return this.http.get<GuestJourneyFunnelDto>(`${this.apiUrl}/guest-journey-funnel`, { params });
  }

  /**
   * Get correlation between response time and guest satisfaction
   */
  getResponseSatisfactionCorrelation(startDate?: string, endDate?: string): Observable<ResponseSatisfactionCorrelationDto> {
    const params = this.buildDateParams(startDate, endDate);
    return this.http.get<ResponseSatisfactionCorrelationDto>(`${this.apiUrl}/response-satisfaction-correlation`, { params });
  }

  /**
   * Get WhatsApp escalation metrics
   */
  getWhatsAppEscalation(startDate?: string, endDate?: string): Observable<WhatsAppEscalationDto> {
    const params = this.buildDateParams(startDate, endDate);
    return this.http.get<WhatsAppEscalationDto>(`${this.apiUrl}/whatsapp-escalation`, { params });
  }

  /**
   * Get upselling performance metrics
   */
  getUpsellPerformance(startDate?: string, endDate?: string): Observable<UpsellPerformanceDto> {
    const params = this.buildDateParams(startDate, endDate);
    return this.http.get<UpsellPerformanceDto>(`${this.apiUrl}/upsell-performance`, { params });
  }

  /**
   * Convert date range preset to API-compatible strings
   */
  getDateRangeFromPreset(preset: DateRangePreset): { startDate: string; endDate: string } {
    switch (preset) {
      case '7days':
        return { startDate: '7daysAgo', endDate: 'today' };
      case '30days':
        return { startDate: '30daysAgo', endDate: 'today' };
      case '90days':
        return { startDate: '90daysAgo', endDate: 'today' };
      default:
        return { startDate: '30daysAgo', endDate: 'today' };
    }
  }

  /**
   * Build HTTP params for date range
   */
  private buildDateParams(startDate?: string, endDate?: string): HttpParams {
    let params = new HttpParams();
    if (startDate) {
      params = params.set('startDate', startDate);
    }
    if (endDate) {
      params = params.set('endDate', endDate);
    }
    return params;
  }

  /**
   * Format percentage for display
   */
  formatPercent(value: number, decimals: number = 1): string {
    return `${value.toFixed(decimals)}%`;
  }

  /**
   * Format currency for display
   */
  formatCurrency(value: number, currency: string = 'ZAR'): string {
    return new Intl.NumberFormat('en-ZA', {
      style: 'currency',
      currency
    }).format(value);
  }

  /**
   * Get color based on escalation rate (lower is better)
   */
  getEscalationRateColor(rate: number): string {
    if (rate <= 10) return '#28a745'; // Green - excellent
    if (rate <= 25) return '#17a2b8'; // Blue - good
    if (rate <= 40) return '#ffc107'; // Yellow - needs attention
    return '#dc3545'; // Red - critical
  }

  /**
   * Get color based on satisfaction rating (higher is better)
   */
  getSatisfactionColor(rating: number): string {
    if (rating >= 4.5) return '#28a745'; // Green - excellent
    if (rating >= 4.0) return '#17a2b8'; // Blue - good
    if (rating >= 3.0) return '#ffc107'; // Yellow - needs attention
    return '#dc3545'; // Red - critical
  }

  /**
   * Get color based on conversion rate (higher is better)
   */
  getConversionColor(rate: number): string {
    if (rate >= 30) return '#28a745'; // Green - excellent
    if (rate >= 15) return '#17a2b8'; // Blue - good
    if (rate >= 5) return '#ffc107'; // Yellow - needs attention
    return '#dc3545'; // Red - critical
  }
}
