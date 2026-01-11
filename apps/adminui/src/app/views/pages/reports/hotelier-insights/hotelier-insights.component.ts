import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  HotelierReportsService,
  ServiceDemandHeatmapDto,
  MaintenanceTrendsDto,
  GuestJourneyFunnelDto,
  ResponseSatisfactionCorrelationDto,
  WhatsAppEscalationDto,
  UpsellPerformanceDto,
  DateRangePreset
} from '../../../../core/services/hotelier-reports.service';
import { NgApexchartsModule } from 'ng-apexcharts';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexXAxis,
  ApexYAxis,
  ApexDataLabels,
  ApexPlotOptions,
  ApexTooltip,
  ApexLegend,
  ApexNonAxisChartSeries,
  ApexFill
} from 'ng-apexcharts';

@Component({
  selector: 'app-hotelier-insights',
  standalone: true,
  imports: [CommonModule, FormsModule, NgApexchartsModule],
  template: `
    <div class="container-fluid">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h2 class="mb-1">Hotelier Insights</h2>
          <p class="text-muted mb-0">
            Strategic operational intelligence for hotel management
          </p>
        </div>
        <div class="d-flex gap-2">
          <select class="form-select" [(ngModel)]="selectedRange" (ngModelChange)="onRangeChange($event)">
            <option value="7days">Last 7 Days</option>
            <option value="30days">Last 30 Days</option>
            <option value="90days">Last 90 Days</option>
          </select>
          <button class="btn btn-outline-secondary" (click)="refreshData()" [disabled]="loading()">
            <i class="cil-reload"></i> Refresh
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
          <p class="mt-2 text-muted">Loading insights data...</p>
        </div>
      } @else {
        <!-- Row 1: Service Demand & Maintenance -->
        <div class="row g-4 mb-4">
          <!-- Service Demand Heatmap -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Service Demand Heatmap</h5>
                <small class="text-muted">
                  Peak: {{ heatmap()?.peakDay }} at {{ heatmap()?.peakHour }}:00
                </small>
              </div>
              <div class="card-body">
                @if (heatmapChartSeries().length > 0) {
                  <apx-chart
                    [series]="heatmapChartSeries()"
                    [chart]="heatmapChartOptions.chart!"
                    [dataLabels]="heatmapChartOptions.dataLabels!"
                    [plotOptions]="heatmapChartOptions.plotOptions!"
                    [xaxis]="heatmapChartOptions.xaxis!"
                    [yaxis]="heatmapChartOptions.yaxis!"
                    [tooltip]="heatmapChartOptions.tooltip!"
                    [legend]="heatmapChartOptions.legend!"
                  ></apx-chart>
                } @else {
                  <div class="text-center py-4 text-muted">
                    <i class="cil-chart display-4"></i>
                    <p class="mt-2">No data available</p>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Maintenance Trends -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Maintenance Trends</h5>
              </div>
              <div class="card-body">
                <!-- Summary Stats -->
                <div class="row g-3 mb-3">
                  <div class="col-4 text-center">
                    <h4 class="mb-0">{{ maintenance()?.totalIssues | number }}</h4>
                    <small class="text-muted">Total Issues</small>
                  </div>
                  <div class="col-4 text-center">
                    <h4 class="mb-0">{{ maintenance()?.avgResolutionHours | number:'1.1-1' }}h</h4>
                    <small class="text-muted">Avg Resolution</small>
                  </div>
                  <div class="col-4 text-center">
                    <h4 class="mb-0 text-warning">{{ maintenance()?.repeatRate | number:'1.1-1' }}%</h4>
                    <small class="text-muted">Repeat Rate</small>
                  </div>
                </div>

                <!-- Category Breakdown -->
                <h6 class="mb-2">By Category</h6>
                <div class="table-responsive" style="max-height: 200px;">
                  <table class="table table-sm table-hover mb-0">
                    <thead class="sticky-top bg-white">
                      <tr>
                        <th>Category</th>
                        <th class="text-end">Count</th>
                        <th class="text-end">Trend</th>
                        <th class="text-end">Avg Time</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (cat of maintenance()?.byCategory || []; track cat.category) {
                        <tr>
                          <td>{{ cat.category }}</td>
                          <td class="text-end">{{ cat.count }}</td>
                          <td class="text-end">
                            <span [class]="cat.changePercent >= 0 ? 'text-danger' : 'text-success'">
                              {{ cat.changePercent >= 0 ? '+' : '' }}{{ cat.changePercent | number:'1.0-0' }}%
                            </span>
                          </td>
                          <td class="text-end">{{ cat.avgResolutionHours | number:'1.1-1' }}h</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>

                <!-- Repeat Issues Alert -->
                @if (maintenance()?.repeatIssues?.length) {
                  <div class="alert alert-warning mt-3 mb-0 small">
                    <strong>Repeat Issues:</strong>
                    @for (issue of maintenance()?.repeatIssues?.slice(0, 3) || []; track issue.roomNumber + issue.category) {
                      Room {{ issue.roomNumber }} - {{ issue.category }} ({{ issue.occurrenceCount }}x){{ $last ? '' : ', ' }}
                    }
                  </div>
                }
              </div>
            </div>
          </div>
        </div>

        <!-- Row 2: Journey Funnel & Upsell -->
        <div class="row g-4 mb-4">
          <!-- Guest Journey Funnel -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Guest Journey Funnel</h5>
                <span class="badge bg-info">
                  {{ funnel()?.overallConversionRate | number:'1.1-1' }}% conversion
                </span>
              </div>
              <div class="card-body">
                @for (stage of funnel()?.stages || []; track stage.name; let i = $index) {
                  <div class="funnel-stage mb-3">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                      <span class="fw-medium">{{ stage.name }}</span>
                      <span>{{ stage.count | number }}</span>
                    </div>
                    <div class="progress" style="height: 24px;">
                      <div class="progress-bar"
                           [style.width.%]="stage.percentOfTotal"
                           [style.background-color]="getFunnelColor(i)">
                        {{ stage.percentOfTotal | number:'1.0-0' }}%
                      </div>
                    </div>
                    <div class="d-flex justify-content-between mt-1">
                      <small class="text-muted">{{ stage.description }}</small>
                      @if (stage.dropOffRate > 0) {
                        <small class="text-danger">{{ stage.dropOffRate | number:'1.0-0' }}% drop-off</small>
                      }
                    </div>
                  </div>
                }
                @if (funnel()?.biggestDropOff) {
                  <div class="alert alert-info mb-0 small mt-3">
                    <strong>Insight:</strong> Biggest drop-off at "{{ funnel()?.biggestDropOff }}"
                    ({{ funnel()?.biggestDropOffPercent | number:'1.0-0' }}%)
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Upsell Performance -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header">
                <h5 class="mb-0">Upselling Performance</h5>
              </div>
              <div class="card-body">
                <!-- Summary Stats -->
                <div class="row g-3 mb-3">
                  <div class="col-4 text-center">
                    <h4 class="mb-0">{{ upsell()?.totalSuggestions | number }}</h4>
                    <small class="text-muted">Suggestions</small>
                  </div>
                  <div class="col-4 text-center">
                    <h4 class="mb-0 text-success">{{ upsell()?.conversionRate | number:'1.1-1' }}%</h4>
                    <small class="text-muted">Conversion</small>
                  </div>
                  <div class="col-4 text-center">
                    <h4 class="mb-0 text-primary">{{ formatCurrency(upsell()?.totalRevenue || 0) }}</h4>
                    <small class="text-muted">Revenue</small>
                  </div>
                </div>

                <!-- Top Services -->
                <h6 class="mb-2">Top Converting Services</h6>
                <div class="table-responsive" style="max-height: 180px;">
                  <table class="table table-sm table-hover mb-0">
                    <thead class="sticky-top bg-white">
                      <tr>
                        <th>Service</th>
                        <th class="text-end">Conv %</th>
                        <th class="text-end">Revenue</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (svc of upsell()?.topServices?.slice(0, 5) || []; track svc.serviceName) {
                        <tr>
                          <td>{{ svc.serviceName }}</td>
                          <td class="text-end">
                            <span [class]="getConversionColorClass(svc.conversionRate)">
                              {{ svc.conversionRate | number:'1.1-1' }}%
                            </span>
                          </td>
                          <td class="text-end">{{ formatCurrency(svc.revenue) }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Row 3: Response/Satisfaction & WhatsApp -->
        <div class="row g-4 mb-4">
          <!-- Response vs Satisfaction -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Response Time vs Satisfaction</h5>
                <span class="badge" [class]="getCorrelationBadgeClass()">
                  {{ correlation()?.correlationDescription }}
                </span>
              </div>
              <div class="card-body">
                @if (correlationChartSeries().length > 0) {
                  <apx-chart
                    [series]="correlationChartSeries()"
                    [chart]="correlationChartOptions.chart!"
                    [xaxis]="correlationChartOptions.xaxis!"
                    [yaxis]="correlationChartOptions.yaxis!"
                    [plotOptions]="correlationChartOptions.plotOptions!"
                    [dataLabels]="correlationChartOptions.dataLabels!"
                    [tooltip]="correlationChartOptions.tooltip!"
                  ></apx-chart>
                }

                <!-- Key Insight -->
                <div class="row g-3 mt-2 text-center">
                  <div class="col-6">
                    <div class="border rounded p-2">
                      <h5 class="mb-0 text-success">{{ correlation()?.avgRatingUnder10Min | number:'1.1-1' }}</h5>
                      <small class="text-muted">Rating (under 10min)</small>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2">
                      <h5 class="mb-0 text-danger">{{ correlation()?.avgRatingOver30Min | number:'1.1-1' }}</h5>
                      <small class="text-muted">Rating (over 30min)</small>
                    </div>
                  </div>
                </div>
                @if (correlation()?.insight) {
                  <div class="alert alert-success mb-0 mt-3 small">
                    <strong>Insight:</strong> {{ correlation()?.insight }}
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- WhatsApp Escalation -->
          <div class="col-xl-6">
            <div class="card h-100">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">WhatsApp Escalation</h5>
                <span class="badge" [class]="getEscalationBadgeClass()">
                  {{ escalation()?.escalationRate | number:'1.1-1' }}% escalation rate
                </span>
              </div>
              <div class="card-body">
                <!-- Donut Chart -->
                <div class="row">
                  <div class="col-5">
                    @if (escalationDonutSeries().length > 0) {
                      <apx-chart
                        [series]="escalationDonutSeries()"
                        [chart]="escalationDonutOptions.chart!"
                        [labels]="escalationDonutOptions.labels!"
                        [colors]="escalationDonutOptions.colors!"
                        [legend]="escalationDonutOptions.legend!"
                        [plotOptions]="escalationDonutOptions.plotOptions!"
                      ></apx-chart>
                    }
                  </div>
                  <div class="col-7">
                    <div class="mb-2">
                      <div class="d-flex justify-content-between">
                        <span class="text-success"><i class="cil-check-circle"></i> Bot Resolved</span>
                        <strong>{{ escalation()?.botResolved | number }}</strong>
                      </div>
                    </div>
                    <div class="mb-2">
                      <div class="d-flex justify-content-between">
                        <span class="text-warning"><i class="cil-user"></i> Escalated</span>
                        <strong>{{ escalation()?.escalatedToAgent | number }}</strong>
                      </div>
                    </div>
                    <div class="mb-3">
                      <div class="d-flex justify-content-between">
                        <span><i class="cil-speech"></i> Total</span>
                        <strong>{{ escalation()?.totalConversations | number }}</strong>
                      </div>
                    </div>

                    @if (escalation()?.portalEscalations && escalation()!.portalEscalations! > 0) {
                      <div class="mb-3 pt-2 border-top">
                        <div class="d-flex justify-content-between">
                          <span class="text-info"><i class="cil-mobile"></i> Portal Contacts</span>
                          <strong>{{ escalation()?.portalEscalations | number }}</strong>
                        </div>
                        <small class="text-muted">Guests who clicked "Contact Us" on portal</small>
                      </div>
                    }

                    <h6 class="small text-muted mb-2">Top Escalation Reasons</h6>
                    @for (intent of escalation()?.byIntent?.slice(0, 3) || []; track intent.intent) {
                      <div class="mb-1 small">
                        <div class="d-flex justify-content-between">
                          <span>{{ intent.intent }}</span>
                          <span class="text-warning">{{ intent.escalationRate | number:'1.0-0' }}%</span>
                        </div>
                      </div>
                    }
                  </div>
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
    .funnel-stage .progress {
      border-radius: 4px;
    }
    .funnel-stage .progress-bar {
      color: white;
      font-weight: 500;
      font-size: 0.75rem;
      line-height: 24px;
    }
    .table th {
      font-weight: 600;
      color: #6c757d;
      font-size: 0.8rem;
      border-top: none;
    }
    .table td {
      font-size: 0.875rem;
    }
    .sticky-top {
      z-index: 1;
    }
  `]
})
export class HotelierInsightsComponent implements OnInit {
  private reportsService = inject(HotelierReportsService);

  loading = signal(true);
  heatmap = signal<ServiceDemandHeatmapDto | null>(null);
  maintenance = signal<MaintenanceTrendsDto | null>(null);
  funnel = signal<GuestJourneyFunnelDto | null>(null);
  correlation = signal<ResponseSatisfactionCorrelationDto | null>(null);
  escalation = signal<WhatsAppEscalationDto | null>(null);
  upsell = signal<UpsellPerformanceDto | null>(null);

  selectedRange: DateRangePreset = '30days';

  // Heatmap Chart
  heatmapChartSeries = computed<ApexAxisChartSeries>(() => {
    const data = this.heatmap()?.data || [];
    if (!data.length) return [];

    // Group by day, create series for each day
    const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    return days.map((day, dayIndex) => ({
      name: day,
      data: Array.from({ length: 24 }, (_, hour) => {
        const cell = data.find(d => d.dayOfWeek === dayIndex && d.hour === hour);
        return cell?.count || 0;
      })
    }));
  });

  heatmapChartOptions: {
    chart: ApexChart;
    dataLabels: ApexDataLabels;
    plotOptions: ApexPlotOptions;
    xaxis: ApexXAxis;
    yaxis: ApexYAxis;
    tooltip: ApexTooltip;
    legend: ApexLegend;
  } = {
    chart: {
      type: 'heatmap',
      height: 280,
      toolbar: { show: false },
      foreColor: '#a0aec0'
    },
    dataLabels: { enabled: false },
    plotOptions: {
      heatmap: {
        shadeIntensity: 0.5,
        colorScale: {
          ranges: [
            { from: 0, to: 0, color: '#2d3748', name: 'None' },
            { from: 1, to: 5, color: '#48bb78', name: 'Low' },
            { from: 6, to: 15, color: '#38a169', name: 'Medium' },
            { from: 16, to: 50, color: '#2f855a', name: 'High' },
            { from: 51, to: 1000, color: '#276749', name: 'Very High' }
          ]
        }
      }
    },
    xaxis: {
      categories: Array.from({ length: 24 }, (_, i) => `${i}:00`),
      labels: {
        rotate: -45,
        style: { fontSize: '10px', colors: '#a0aec0' }
      }
    },
    yaxis: {
      labels: {
        style: { colors: '#a0aec0' }
      }
    },
    tooltip: {
      theme: 'dark',
      y: {
        formatter: (val: number) => `${val} requests`
      }
    },
    legend: { show: false }
  };

  // Correlation Chart
  correlationChartSeries = computed<ApexAxisChartSeries>(() => {
    const points = this.correlation()?.dataPoints || [];
    if (!points.length) return [];
    return [{
      name: 'Avg Rating',
      data: points.map(p => ({
        x: p.responseTimeBucket,
        y: p.avgRating
      }))
    }];
  });

  correlationChartOptions: {
    chart: ApexChart;
    xaxis: ApexXAxis;
    yaxis: ApexYAxis;
    plotOptions: ApexPlotOptions;
    dataLabels: ApexDataLabels;
    tooltip: ApexTooltip;
  } = {
    chart: {
      type: 'bar',
      height: 200,
      toolbar: { show: false },
      foreColor: '#a0aec0'
    },
    xaxis: {
      type: 'category',
      labels: {
        style: { colors: '#a0aec0', fontSize: '11px' }
      }
    },
    yaxis: {
      min: 0,
      max: 5,
      labels: {
        formatter: (val: number) => val.toFixed(1),
        style: { colors: '#a0aec0' }
      }
    },
    plotOptions: {
      bar: {
        borderRadius: 4,
        columnWidth: '60%',
        colors: {
          ranges: [
            { from: 0, to: 3, color: '#dc3545' },
            { from: 3, to: 4, color: '#ffc107' },
            { from: 4, to: 5, color: '#28a745' }
          ]
        }
      }
    },
    dataLabels: {
      enabled: true,
      formatter: (val: number) => val.toFixed(1),
      style: { colors: ['#ffffff'] }
    },
    tooltip: {
      theme: 'dark',
      y: { formatter: (val: number) => `${val.toFixed(2)} stars` }
    }
  };

  // Escalation Donut
  escalationDonutSeries = computed<ApexNonAxisChartSeries>(() => {
    const data = this.escalation();
    if (!data) return [];
    return [data.botResolved, data.escalatedToAgent];
  });

  escalationDonutOptions: {
    chart: ApexChart;
    labels: string[];
    colors: string[];
    legend: ApexLegend;
    plotOptions: ApexPlotOptions;
  } = {
    chart: {
      type: 'donut',
      height: 180,
      foreColor: '#a0aec0'
    },
    labels: ['Bot Resolved', 'Escalated'],
    colors: ['#28a745', '#ffc107'],
    legend: { show: false },
    plotOptions: {
      pie: {
        donut: {
          size: '65%',
          labels: {
            show: true,
            name: {
              color: '#a0aec0'
            },
            value: {
              color: '#e2e8f0',
              fontSize: '16px',
              fontWeight: 600
            },
            total: {
              show: true,
              label: 'Bot Success',
              color: '#a0aec0',
              formatter: () => {
                const data = this.escalation();
                return data ? `${data.botSuccessRate.toFixed(0)}%` : '0%';
              }
            }
          }
        }
      }
    }
  };

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const { startDate, endDate } = this.reportsService.getDateRangeFromPreset(this.selectedRange);
    this.loading.set(true);

    Promise.all([
      this.reportsService.getServiceDemandHeatmap(startDate, endDate).toPromise(),
      this.reportsService.getMaintenanceTrends(startDate, endDate).toPromise(),
      this.reportsService.getGuestJourneyFunnel(startDate, endDate).toPromise(),
      this.reportsService.getResponseSatisfactionCorrelation(startDate, endDate).toPromise(),
      this.reportsService.getWhatsAppEscalation(startDate, endDate).toPromise(),
      this.reportsService.getUpsellPerformance(startDate, endDate).toPromise()
    ]).then(([heatmap, maintenance, funnel, correlation, escalation, upsell]) => {
      this.heatmap.set(heatmap || null);
      this.maintenance.set(maintenance || null);
      this.funnel.set(funnel || null);
      this.correlation.set(correlation || null);
      this.escalation.set(escalation || null);
      this.upsell.set(upsell || null);
      this.loading.set(false);
    }).catch(error => {
      console.error('Error loading hotelier insights:', error);
      this.loading.set(false);
    });
  }

  onRangeChange(range: DateRangePreset): void {
    this.selectedRange = range;
    this.loadData();
  }

  refreshData(): void {
    this.loadData();
  }

  getFunnelColor(index: number): string {
    const colors = ['#0d6efd', '#198754', '#ffc107', '#dc3545', '#6c757d'];
    return colors[index % colors.length];
  }

  formatCurrency(value: number): string {
    return this.reportsService.formatCurrency(value);
  }

  getConversionColorClass(rate: number): string {
    if (rate >= 30) return 'text-success fw-bold';
    if (rate >= 15) return 'text-info';
    if (rate >= 5) return 'text-warning';
    return 'text-danger';
  }

  getCorrelationBadgeClass(): string {
    const strength = this.correlation()?.correlationStrength || 0;
    if (Math.abs(strength) >= 0.5) return 'bg-success';
    if (Math.abs(strength) >= 0.3) return 'bg-info';
    return 'bg-secondary';
  }

  getEscalationBadgeClass(): string {
    const rate = this.escalation()?.escalationRate || 0;
    if (rate <= 15) return 'bg-success';
    if (rate <= 30) return 'bg-info';
    if (rate <= 50) return 'bg-warning';
    return 'bg-danger';
  }
}
