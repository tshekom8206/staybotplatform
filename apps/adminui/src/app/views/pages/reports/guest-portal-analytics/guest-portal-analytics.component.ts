import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  GA4AnalyticsService,
  GA4Overview,
  PageView,
  PageViewTimeSeries,
  GA4Event,
  Engagement,
  GA4Status
} from '../../../../core/services/ga4-analytics.service';
import { NgApexchartsModule } from 'ng-apexcharts';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexXAxis,
  ApexDataLabels,
  ApexStroke,
  ApexGrid,
  ApexTooltip,
  ApexFill
} from 'ng-apexcharts';

type DateRange = '7d' | '30d' | '90d' | 'custom';

@Component({
  selector: 'app-guest-portal-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, NgApexchartsModule],
  template: `
    <div class="container-fluid">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h2 class="mb-1">Guest Portal Analytics</h2>
          <p class="text-muted mb-0">
            Track guest engagement with your hotel's guest portal
          </p>
        </div>
        <div class="d-flex gap-2">
          <select class="form-select" [(ngModel)]="selectedRange" (ngModelChange)="onRangeChange($event)">
            <option value="7d">Last 7 Days</option>
            <option value="30d">Last 30 Days</option>
            <option value="90d">Last 90 Days</option>
          </select>
          <button class="btn btn-outline-secondary" (click)="refreshData()">
            <i class="cil-reload"></i> Refresh
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
          <p class="mt-2 text-muted">Loading analytics data...</p>
        </div>
      } @else if (errorMessage()) {
        <div class="alert alert-danger">
          <i class="cil-warning me-2"></i>
          {{ errorMessage() }}
          <button class="btn btn-sm btn-outline-danger ms-3" (click)="refreshData()">
            <i class="cil-reload"></i> Retry
          </button>
        </div>
      } @else {
        <!-- Overview Cards -->
        <div class="row g-4 mb-4">
          <div class="col-md-6 col-xl-3">
            <div class="card h-100">
              <div class="card-body">
                <div class="d-flex justify-content-between align-items-start">
                  <div>
                    <h6 class="text-muted mb-2">Sessions</h6>
                    <h3 class="mb-0">{{ overview()?.sessions | number }}</h3>
                  </div>
                  <div class="badge rounded-pill" [ngClass]="getChangeClass(overview()?.sessionsChange)">
                    {{ getChangeDisplay(overview()?.sessionsChange) }}
                  </div>
                </div>
                <small class="text-muted">vs previous period</small>
              </div>
            </div>
          </div>
          <div class="col-md-6 col-xl-3">
            <div class="card h-100">
              <div class="card-body">
                <div class="d-flex justify-content-between align-items-start">
                  <div>
                    <h6 class="text-muted mb-2">Active Users</h6>
                    <h3 class="mb-0">{{ overview()?.activeUsers | number }}</h3>
                  </div>
                  <div class="badge rounded-pill" [ngClass]="getChangeClass(overview()?.usersChange)">
                    {{ getChangeDisplay(overview()?.usersChange) }}
                  </div>
                </div>
                <small class="text-muted">vs previous period</small>
              </div>
            </div>
          </div>
          <div class="col-md-6 col-xl-3">
            <div class="card h-100">
              <div class="card-body">
                <div class="d-flex justify-content-between align-items-start">
                  <div>
                    <h6 class="text-muted mb-2">Page Views</h6>
                    <h3 class="mb-0">{{ overview()?.pageViews | number }}</h3>
                  </div>
                  <div class="badge rounded-pill" [ngClass]="getChangeClass(overview()?.pageViewsChange)">
                    {{ getChangeDisplay(overview()?.pageViewsChange) }}
                  </div>
                </div>
                <small class="text-muted">vs previous period</small>
              </div>
            </div>
          </div>
          <div class="col-md-6 col-xl-3">
            <div class="card h-100">
              <div class="card-body">
                <div>
                  <h6 class="text-muted mb-2">Engagement Rate</h6>
                  <h3 class="mb-0">{{ (overview()?.engagementRate || 0) * 100 | number:'1.1-1' }}%</h3>
                </div>
                <div class="progress mt-2" style="height: 6px;">
                  <div class="progress-bar bg-success" [style.width.%]="(overview()?.engagementRate || 0) * 100"></div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="row g-4">
          <!-- Page Views Chart -->
          <div class="col-xl-8">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Page Views Over Time</h5>
              </div>
              <div class="card-body">
                @if (chartSeries().length > 0) {
                  <apx-chart
                    [series]="chartSeries()"
                    [chart]="chartOptions.chart!"
                    [xaxis]="chartOptions.xaxis!"
                    [dataLabels]="chartOptions.dataLabels!"
                    [stroke]="chartOptions.stroke!"
                    [grid]="chartOptions.grid!"
                    [tooltip]="chartOptions.tooltip!"
                    [fill]="chartOptions.fill!"
                  ></apx-chart>
                } @else {
                  <div class="text-center py-4 text-muted">
                    <i class="cil-chart-line display-4"></i>
                    <p class="mt-2">No chart data available</p>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Engagement Metrics -->
          <div class="col-xl-4">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Engagement Metrics</h5>
              </div>
              <div class="card-body">
                <div class="mb-4">
                  <div class="d-flex justify-content-between mb-1">
                    <span>Avg. Session Duration</span>
                    <strong>{{ formatDuration(engagement()?.avgSessionDuration) }}</strong>
                  </div>
                </div>
                <div class="mb-4">
                  <div class="d-flex justify-content-between mb-1">
                    <span>Bounce Rate</span>
                    <strong>{{ (engagement()?.bounceRate || 0) * 100 | number:'1.1-1' }}%</strong>
                  </div>
                  <div class="progress" style="height: 6px;">
                    <div class="progress-bar bg-warning" [style.width.%]="(engagement()?.bounceRate || 0) * 100"></div>
                  </div>
                </div>
                <div class="mb-4">
                  <div class="d-flex justify-content-between mb-1">
                    <span>Pages per Session</span>
                    <strong>{{ engagement()?.avgPagesPerSession | number:'1.1-1' }}</strong>
                  </div>
                </div>
                <div>
                  <div class="d-flex justify-content-between mb-1">
                    <span>Engaged Sessions</span>
                    <strong>{{ engagement()?.engagedSessions | number }}</strong>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="row g-4 mt-0">
          <!-- Top Pages -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Top Pages</h5>
              </div>
              <div class="card-body p-0">
                <div class="table-responsive">
                  <table class="table table-hover mb-0">
                    <thead>
                      <tr>
                        <th>Page</th>
                        <th class="text-end">Views</th>
                        <th class="text-end">Unique</th>
                        <th class="text-end">Avg. Time</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (page of topPages(); track page.pagePath) {
                        <tr>
                          <td>
                            <div class="fw-medium">{{ page.pageTitle }}</div>
                            <small class="text-muted">{{ page.pagePath }}</small>
                          </td>
                          <td class="text-end">{{ page.views | number }}</td>
                          <td class="text-end">{{ page.uniqueViews | number }}</td>
                          <td class="text-end">{{ formatDuration(page.avgTimeOnPage) }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="4" class="text-center text-muted py-3">No page data available</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>

          <!-- Events -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Guest Actions</h5>
              </div>
              <div class="card-body p-0">
                <div class="table-responsive">
                  <table class="table table-hover mb-0">
                    <thead>
                      <tr>
                        <th>Event</th>
                        <th class="text-end">Count</th>
                        <th class="text-end">Unique Users</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (event of events(); track event.eventName) {
                        <tr>
                          <td>
                            <div class="d-flex align-items-center gap-2">
                              <i [class]="getEventIcon(event.eventName)" class="text-primary"></i>
                              <span>{{ formatEventName(event.eventName) }}</span>
                            </div>
                          </td>
                          <td class="text-end">{{ event.count | number }}</td>
                          <td class="text-end">{{ event.uniqueUsers | number }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="3" class="text-center text-muted py-3">No event data available</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .card {
      border: none;
      box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
    }
    .card-header {
      background: transparent;
      border-bottom: 1px solid rgba(0, 0, 0, 0.125);
    }
    .badge.bg-success-soft {
      background-color: rgba(25, 135, 84, 0.1);
      color: #198754;
    }
    .badge.bg-danger-soft {
      background-color: rgba(220, 53, 69, 0.1);
      color: #dc3545;
    }
    .table th {
      font-weight: 600;
      color: #6c757d;
      border-top: none;
    }
  `]
})
export class GuestPortalAnalyticsComponent implements OnInit {
  private ga4Service = inject(GA4AnalyticsService);

  loading = signal(true);
  errorMessage = signal<string | null>(null);
  status = signal<GA4Status | null>(null);
  overview = signal<GA4Overview | null>(null);
  topPages = signal<PageView[]>([]);
  pageViewsTimeSeries = signal<PageViewTimeSeries[]>([]);
  events = signal<GA4Event[]>([]);
  engagement = signal<Engagement | null>(null);

  selectedRange: DateRange = '7d';

  chartSeries = computed<ApexAxisChartSeries>(() => {
    const data = this.pageViewsTimeSeries();
    if (!data.length) return [];
    return [
      {
        name: 'Page Views',
        data: data.map(d => d.pageViews)
      },
      {
        name: 'Sessions',
        data: data.map(d => d.sessions)
      }
    ];
  });

  chartOptions: {
    chart: ApexChart;
    xaxis: ApexXAxis;
    dataLabels: ApexDataLabels;
    stroke: ApexStroke;
    grid: ApexGrid;
    tooltip: ApexTooltip;
    fill: ApexFill;
  } = {
    chart: {
      type: 'area',
      height: 350,
      toolbar: { show: false },
      zoom: { enabled: false }
    },
    xaxis: {
      type: 'category',
      categories: [],
      labels: {
        formatter: (val: string) => {
          if (!val) return '';
          const date = new Date(val);
          return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        }
      }
    },
    dataLabels: { enabled: false },
    stroke: { curve: 'smooth', width: 2 },
    grid: {
      borderColor: '#f1f1f1',
      strokeDashArray: 4
    },
    tooltip: {
      x: {
        formatter: (val: number) => {
          const data = this.pageViewsTimeSeries();
          if (data[val - 1]) {
            return new Date(data[val - 1].date).toLocaleDateString();
          }
          return '';
        }
      }
    },
    fill: {
      type: 'gradient',
      gradient: {
        shadeIntensity: 1,
        opacityFrom: 0.4,
        opacityTo: 0.1
      }
    }
  };

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const { startDate, endDate } = this.getDateRange();
    this.loading.set(true);
    this.errorMessage.set(null);

    // Load all data in parallel
    Promise.all([
      this.ga4Service.getStatus().toPromise(),
      this.ga4Service.getOverview(startDate, endDate).toPromise(),
      this.ga4Service.getTopPages(startDate, endDate).toPromise(),
      this.ga4Service.getPageViews(startDate, endDate).toPromise(),
      this.ga4Service.getEvents(startDate, endDate).toPromise(),
      this.ga4Service.getEngagement(startDate, endDate).toPromise()
    ]).then(([status, overview, topPages, pageViews, events, engagement]) => {
      this.status.set(status || null);
      this.overview.set(overview || null);
      this.topPages.set(topPages || []);
      this.pageViewsTimeSeries.set(pageViews || []);
      this.events.set(events || []);
      this.engagement.set(engagement || null);

      // Update chart categories
      if (pageViews) {
        this.chartOptions.xaxis!.categories = pageViews.map(d => d.date);
      }

      this.loading.set(false);
    }).catch((error: any) => {
      console.error('Error loading GA4 data:', error);
      const errorDetails = error?.error?.details || error?.error?.error || error?.message || 'Unknown error';
      this.errorMessage.set(`Failed to load GA4 analytics: ${errorDetails}`);
      this.loading.set(false);
    });
  }

  onRangeChange(range: DateRange): void {
    this.selectedRange = range;
    this.loadData();
  }

  refreshData(): void {
    this.loadData();
  }

  private getDateRange(): { startDate: string; endDate: string } {
    switch (this.selectedRange) {
      case '7d':
        return { startDate: '7daysAgo', endDate: 'today' };
      case '30d':
        return { startDate: '30daysAgo', endDate: 'today' };
      case '90d':
        return { startDate: '90daysAgo', endDate: 'today' };
      default:
        return { startDate: '7daysAgo', endDate: 'today' };
    }
  }

  getChangeClass(change: number | undefined): string {
    if (!change) return 'bg-secondary';
    return change >= 0 ? 'bg-success-soft' : 'bg-danger-soft';
  }

  getChangeDisplay(change: number | undefined): string {
    if (!change) return '0%';
    const sign = change >= 0 ? '+' : '';
    return `${sign}${change.toFixed(1)}%`;
  }

  formatDuration(seconds: number | undefined): string {
    if (!seconds) return '0s';
    if (seconds < 60) return `${Math.round(seconds)}s`;
    const minutes = Math.floor(seconds / 60);
    const secs = Math.round(seconds % 60);
    return `${minutes}m ${secs}s`;
  }

  formatEventName(eventName: string): string {
    return eventName
      .replace(/_/g, ' ')
      .replace(/\b\w/g, l => l.toUpperCase());
  }

  getEventIcon(eventName: string): string {
    const icons: Record<string, string> = {
      'page_view': 'cil-browser',
      'menu_click': 'cil-cursor',
      'service_request': 'cil-room',
      'maintenance_request': 'cil-wrench',
      'rating_submitted': 'cil-star',
      'whatsapp_opened': 'cib-whatsapp',
      'featured_service_click': 'cil-gift',
      'lost_item_report': 'cil-magnifying-glass'
    };
    return icons[eventName] || 'cil-notes';
  }
}
