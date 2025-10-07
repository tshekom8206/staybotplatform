import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgbDropdownModule, NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth.service';

interface ServiceUsageData {
  totalServiceRequests: number;
  totalGuestRequests: number;
  serviceRequestRate: number;
  topRequestedServices: Array<{
    serviceId: number;
    serviceName: string;
    category: string;
    requestCount: number;
    completedCount: number;
  }>;
  usageByDepartment: Array<{
    department: string;
    requestCount: number;
    completedCount: number;
  }>;
  avgCompletionByCategory: { [key: string]: number };
  usageByDay: { [key: string]: number };
  usageByHour: { [key: string]: number };
}

@Component({
  selector: 'app-usage',
  standalone: true,
  imports: [
    CommonModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    FeatherIconDirective
  ],
  templateUrl: './usage.component.html',
  styleUrls: ['./usage.component.scss']
})
export class UsageComponent implements OnInit {

  data: ServiceUsageData | null = null;
  loading = true;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  // Chart data
  serviceChartData: Array<{ label: string; value: number; color: string }> = [];
  departmentChartData: Array<{ label: string; value: number; color: string; completionRate: number }> = [];
  categoryChartData: Array<{ label: string; value: number; color: string }> = [];

  private colors = [
    '#25D466', // Primary green
    '#17a2b8', // Info blue
    '#ffc107', // Warning yellow
    '#dc3545', // Danger red
    '#6f42c1', // Purple
    '#fd7e14', // Orange
    '#20c997', // Teal
  ];

  ngOnInit() {
    this.loadServiceUsageData();
  }

  private async loadServiceUsageData() {
    try {
      this.loading = true;
      this.error = null;

      const token = this.authService.getToken();
      if (!token) {
        throw new Error('No authentication token available. Please log in again.');
      }

      const response = await fetch(`${environment.apiUrl}/reports/usage`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Authentication required. Please log in again.');
        } else if (response.status === 403) {
          throw new Error('Access forbidden. You do not have permission to view this report.');
        } else if (response.status === 404) {
          throw new Error('Report endpoint not found.');
        } else {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.message || `Failed to load service usage data (${response.status})`);
        }
      }

      this.data = await response.json();
      this.prepareChartData();
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'An error occurred';
      console.error('Error loading service usage data:', error);
    } finally {
      this.loading = false;
    }
  }

  private prepareChartData() {
    if (!this.data) return;

    // Prepare top services chart data
    this.serviceChartData = this.data.topRequestedServices
      .slice(0, 6) // Top 6 services
      .map((service, index) => ({
        label: service.serviceName,
        value: service.requestCount,
        color: this.colors[index % this.colors.length]
      }));

    // Prepare department chart data
    this.departmentChartData = this.data.usageByDepartment.map((dept, index) => {
      const completionRate = dept.requestCount > 0 ? (dept.completedCount / dept.requestCount) * 100 : 0;
      return {
        label: dept.department,
        value: dept.requestCount,
        color: this.colors[index % this.colors.length],
        completionRate: completionRate
      };
    });

    // Prepare category completion times chart data (convert minutes to hours)
    this.categoryChartData = Object.entries(this.data.avgCompletionByCategory).map(([category, minutes], index) => ({
      label: category,
      value: minutes / 60, // Convert minutes to hours
      color: this.colors[index % this.colors.length]
    }));
  }

  getServiceRequestRateColor(): string {
    if (!this.data) return '#6c757d';
    if (this.data.serviceRequestRate >= 80) return '#25D466';
    if (this.data.serviceRequestRate >= 60) return '#ffc107';
    return '#dc3545';
  }

  getCompletionRateColor(rate: number): string {
    if (rate >= 90) return '#25D466';
    if (rate >= 75) return '#17a2b8';
    if (rate >= 60) return '#ffc107';
    if (rate >= 45) return '#fd7e14';
    return '#dc3545';
  }

  formatCompletionTime(hours: number): string {
    if (hours < 1) {
      return `${Math.round(hours * 60)}m`;
    } else if (hours < 24) {
      return `${hours.toFixed(1)}h`;
    } else {
      const days = Math.floor(hours / 24);
      const remainingHours = Math.round(hours % 24);
      return `${days}d ${remainingHours}h`;
    }
  }

  formatHour(hour: number): string {
    if (hour === 0) return '12 AM';
    if (hour === 12) return '12 PM';
    if (hour < 12) return `${hour} AM`;
    return `${hour - 12} PM`;
  }

  getTrendsFromUsageData(): Array<{ label: string; value: number }> {
    if (!this.data) return [];

    return Object.entries(this.data.usageByDay)
      .map(([date, count]) => ({
        label: new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
        value: count
      }))
      .sort((a, b) => new Date(a.label).getTime() - new Date(b.label).getTime())
      .slice(-7); // Last 7 days
  }

  getPeakHours(): Array<{ label: string; value: number }> {
    if (!this.data) return [];

    return Object.entries(this.data.usageByHour)
      .map(([hour, count]) => ({
        label: this.formatHour(parseInt(hour)),
        value: count
      }))
      .sort((a, b) => b.value - a.value)
      .slice(0, 6); // Top 6 peak hours
  }

  refresh() {
    this.loadServiceUsageData();
  }

  exportData() {
    if (!this.data) return;

    const dataStr = JSON.stringify(this.data, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `service-usage-report-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  trackByLabel(index: number, item: { label: string }): string {
    return item.label;
  }

  trackByService(index: number, item: { serviceName: string }): string {
    return item.serviceName;
  }

  trackByDepartment(index: number, item: { department: string }): string {
    return item.department;
  }

  // Computed properties for service efficiency metrics
  getServiceEfficiencyMetrics() {
    if (!this.data || this.data.topRequestedServices.length === 0) {
      return {
        fastestService: 'No data',
        slowestService: 'No data',
        mostRequestedService: 'No data',
        highestCompletionRate: 0
      };
    }

    const services = this.data.topRequestedServices;
    const mostRequested = services.reduce((prev, current) =>
      prev.requestCount > current.requestCount ? prev : current
    );

    const completionRates = services.map(s => s.requestCount > 0 ? (s.completedCount / s.requestCount) * 100 : 0);
    const highestCompletionRate = Math.max(...completionRates);

    // For fastest/slowest, use completion times from category data
    const categoryEntries = Object.entries(this.data.avgCompletionByCategory);
    if (categoryEntries.length === 0) {
      return {
        fastestService: mostRequested.serviceName,
        slowestService: mostRequested.serviceName,
        mostRequestedService: mostRequested.serviceName,
        highestCompletionRate
      };
    }

    const fastestCategory = categoryEntries.reduce((prev, current) =>
      prev[1] < current[1] ? prev : current
    );
    const slowestCategory = categoryEntries.reduce((prev, current) =>
      prev[1] > current[1] ? prev : current
    );

    return {
      fastestService: fastestCategory[0],
      slowestService: slowestCategory[0],
      mostRequestedService: mostRequested.serviceName,
      highestCompletionRate
    };
  }

  getMaxValue(data: Array<{ value: number }>): number {
    return Math.max(...data.map(item => item.value));
  }

  getMaxTrendValue(): number {
    const trends = this.getTrendsFromUsageData();
    return Math.max(...trends.map(item => item.value), 1);
  }

  getMaxCategoryValue(): number {
    return Math.max(...this.categoryChartData.map(item => item.value), 1);
  }
}