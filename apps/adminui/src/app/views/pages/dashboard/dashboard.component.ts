import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NgbCalendar, NgbDatepickerModule, NgbDateStruct, NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { ApexOptions, NgApexchartsModule } from "ng-apexcharts";
import { Subject, takeUntil } from 'rxjs';
import { FeatherIconDirective } from '../../../core/feather-icon/feather-icon.directive';
import { ThemeCssVariableService, ThemeCssVariablesType } from '../../../core/services/theme-css-variable.service';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService, ConnectionState } from '../../../core/services/signalr.service';
import { DashboardService, RecentActivity, DepartmentTaskCount } from './dashboard.service';
import { StatsCardComponent } from '../../../shared/components/stats-card/stats-card.component';
import { Tenant } from '../../../core/models/user.model';

export interface DashboardStats {
  activeGuests: number;
  pendingTasks: number;
  todayCheckins: number;
  todayCheckouts: number;
  emergencyIncidents: number;
  averageResponseTime: number;
  trends?: {
    activeGuestsTrend: number;
    pendingTasksTrend: number;
    checkinsTrend: number;
    responseTimeTrend: number;
  };
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbDropdownModule,
    FormsModule,
    NgbDatepickerModule,
    NgApexchartsModule,
    FeatherIconDirective,
    StatsCardComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  /**
   * NgbDatepicker
   */
  currentDate: NgbDateStruct = inject(NgbCalendar).getToday();

  /**
   * Dashboard data
   */
  stats: DashboardStats = {
    activeGuests: 0,
    pendingTasks: 0,
    todayCheckins: 0,
    todayCheckouts: 0,
    emergencyIncidents: 0,
    averageResponseTime: 0
  };

  loading = true;
  currentTenant: Tenant | null = null;
  connectionState = ConnectionState.Disconnected;
  selectedActivityPeriod: string = 'day';

  // Additional dashboard metrics
  occupancyData = {
    occupancyPercentage: 0,
    totalRooms: 0,
    occupiedRooms: 0,
    changeFromLastMonth: 0
  };
  completedTasksData = {
    completedToday: 0,
    totalToday: 0,
    completionRate: 0,
    changeFromLastMonth: 0
  };
  guestSatisfactionData = {
    averageRating: 0,
    totalRatings: 0,
    ratingDistribution: {},
    changeFromLastMonth: 0
  };

  // Computed properties for backward compatibility
  get occupancyRate(): string {
    return this.occupancyData.occupancyPercentage + '%';
  }

  get completedTasksToday(): number {
    return this.completedTasksData.completedToday;
  }

  get guestSatisfactionScore(): number {
    return this.guestSatisfactionData.averageRating;
  }

  // Trend getters for template usage
  get occupancyTrend(): number {
    const value = this.occupancyData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : value;
  }

  get completedTasksTrend(): number {
    const value = this.completedTasksData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : value;
  }

  get guestSatisfactionTrend(): number {
    const value = this.guestSatisfactionData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : value;
  }

  // Trend direction getters
  get occupancyTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.occupancyData.changeFromLastMonth;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  get completedTasksTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.completedTasksData.changeFromLastMonth;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  get guestSatisfactionTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.guestSatisfactionData.changeFromLastMonth;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  // Absolute trend values for display
  get occupancyTrendAbs(): number {
    const value = this.occupancyData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get completedTasksTrendAbs(): number {
    const value = this.completedTasksData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get guestSatisfactionTrendAbs(): number {
    const value = this.guestSatisfactionData.changeFromLastMonth;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  // Basic stats trend getters
  get activeGuestsTrendValue(): number {
    const value = this.stats.trends?.activeGuestsTrend || 0;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get activeGuestsTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.stats.trends?.activeGuestsTrend || 0;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  get pendingTasksTrendValue(): number {
    const value = this.stats.trends?.pendingTasksTrend || 0;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get pendingTasksTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.stats.trends?.pendingTasksTrend || 0;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  get checkinsTrendValue(): number {
    const value = this.stats.trends?.checkinsTrend || 0;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get checkinsTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.stats.trends?.checkinsTrend || 0;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'up' : 'down';
  }

  get responseTimeTrendValue(): number {
    const value = this.stats.trends?.responseTimeTrend || 0;
    return isNaN(value) || !isFinite(value) ? 0 : Math.abs(value);
  }

  get responseTimeTrendDirection(): 'up' | 'down' | 'neutral' {
    const value = this.stats.trends?.responseTimeTrend || 0;
    if (isNaN(value) || !isFinite(value)) return 'neutral';
    return value >= 0 ? 'down' : 'up'; // For response time, lower is better, so flip the logic
  }

  // Data for components
  recentActivities: RecentActivity[] = [];
  departmentTasks: DepartmentTaskCount[] = [];

  /**
   * Apex chart options
   */
  public activityChartOptions: ApexOptions | any;
  public taskTrendChartOptions: ApexOptions | any;
  public departmentChartOptions: ApexOptions | any;

  // Legacy chart options (kept for compatibility)
  public guestsChartOptions: ApexOptions | any;
  public tasksChartOptions: ApexOptions | any;
  public responseTimeChartOptions: ApexOptions | any;
  public dailyActivityChartOptions: ApexOptions | any;
  public departmentTasksChartOptions: ApexOptions | any;
  public taskCompletionChartOptions: ApexOptions | any;

  themeCssVariables = inject(ThemeCssVariableService).getThemeCssVariables();

  constructor(
    private authService: AuthService,
    private signalRService: SignalRService,
    private dashboardService: DashboardService
  ) {}

  ngOnInit(): void {
    // Get tenant information
    this.authService.currentTenant
      .pipe(takeUntil(this.destroy$))
      .subscribe(tenant => {
        this.currentTenant = tenant;
      });

    // Monitor connection state
    this.signalRService.connectionState$
      .pipe(takeUntil(this.destroy$))
      .subscribe(state => {
        this.connectionState = state;
      });

    // Load dashboard data
    this.loadDashboardData();

    // Setup real-time updates
    this.setupRealTimeUpdates();

    // Initialize charts
    this.initializeCharts();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadDashboardData(): void {
    console.log('Loading dashboard data for date:', this.currentDate);
    this.loading = true;

    // Convert NgbDateStruct to Date
    const selectedDate = this.currentDate ?
      new Date(this.currentDate.year, this.currentDate.month - 1, this.currentDate.day) :
      new Date();

    console.log('Converted date:', selectedDate);

    // Load dashboard stats
    this.dashboardService.getDashboardStats(selectedDate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.stats = stats;
          this.loading = false;
          this.updateCharts();
        },
        error: (error) => {
          console.error('Error loading dashboard stats:', error);
          this.loading = false;
        }
      });

    // Load recent activities
    console.log('Loading recent activities...');
    this.dashboardService.getRecentActivities(10)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (activities) => {
          console.log('Recent activities loaded:', activities);
          this.recentActivities = activities;
        },
        error: (error) => {
          console.error('Error loading recent activities:', error);
          console.error('Full error object:', error);
        }
      });

    // Load department tasks
    this.dashboardService.getDepartmentTasks(selectedDate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (departments) => {
          this.departmentTasks = departments;
          this.updateDepartmentChart();
        },
        error: (error) => {
          console.error('Error loading department tasks:', error);
          this.updateDepartmentChart();
        }
      });

    // Load hourly activity for the selected date
    this.dashboardService.getHourlyActivity(selectedDate, 'day')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.updateActivityChartForPeriod(response);
        },
        error: (error) => {
          console.error('Error loading hourly activity:', error);
          this.updateActivityChart(new Array(24).fill(0));
        }
      });

    // Load task completion trend
    this.dashboardService.getTaskCompletionTrend(selectedDate, 7)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (trendData) => {
          this.updateTaskTrendChart(trendData);
        },
        error: (error) => {
          console.error('Error loading task completion trend:', error);
          this.updateTaskTrendChart({ completed: new Array(7).fill(0), created: new Array(7).fill(0) });
        }
      });

    // Load room occupancy
    this.dashboardService.getRoomOccupancy(selectedDate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (occupancyData) => {
          this.occupancyData = occupancyData;
        },
        error: (error) => {
          console.error('Error loading room occupancy:', error);
        }
      });

    // Load completed tasks summary
    this.dashboardService.getCompletedTasksSummary(selectedDate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tasksData) => {
          this.completedTasksData = tasksData;
        },
        error: (error) => {
          console.error('Error loading completed tasks:', error);
        }
      });

    // Load guest satisfaction
    const endDate = selectedDate;
    const startDate = new Date(selectedDate);
    startDate.setDate(startDate.getDate() - 30); // Last 30 days

    this.dashboardService.getGuestSatisfaction(startDate, endDate)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (satisfactionData) => {
          this.guestSatisfactionData = satisfactionData;
        },
        error: (error) => {
          console.error('Error loading guest satisfaction:', error);
        }
      });
  }

  private setupRealTimeUpdates(): void {
    // Listen for task updates
    this.signalRService.taskCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.stats.pendingTasks++;
        this.updateCharts();
      });

    this.signalRService.taskCompleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.stats.pendingTasks--;
        this.updateCharts();
      });

    // Listen for emergency alerts
    this.signalRService.emergencyAlert$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.stats.emergencyIncidents++;
        this.updateCharts();
      });
  }

  private initializeCharts(): void {
    // Initialize charts that don't depend on API data
    this.taskTrendChartOptions = this.getTaskCompletionChartOptions(this.themeCssVariables);
    this.departmentChartOptions = this.getDepartmentTasksChartOptions(this.themeCssVariables);

    // Initialize activityChartOptions with empty data to prevent null pointer errors
    this.activityChartOptions = this.getDefaultActivityChartOptions(this.themeCssVariables);

    // Legacy charts (for compatibility)
    this.guestsChartOptions = this.getGuestsChartOptions(this.themeCssVariables);
    this.tasksChartOptions = this.getTasksChartOptions(this.themeCssVariables);
    this.responseTimeChartOptions = this.getResponseTimeChartOptions(this.themeCssVariables);
    this.dailyActivityChartOptions = this.getDailyActivityChartOptions(this.themeCssVariables);
    this.departmentTasksChartOptions = this.getDepartmentTasksChartOptions(this.themeCssVariables);
    this.taskCompletionChartOptions = this.getTaskCompletionChartOptions(this.themeCssVariables);
  }

  /**
   * Set activity period for chart filtering
   */
  setActivityPeriod(period: string): void {
    console.log('Setting activity period to:', period);
    this.selectedActivityPeriod = period;

    const selectedDate = this.currentDate ?
      new Date(this.currentDate.year, this.currentDate.month - 1, this.currentDate.day) :
      new Date();

    console.log('Frontend - calling API with selectedDate:', selectedDate, 'period:', period);

    this.dashboardService.getHourlyActivity(selectedDate, period)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          console.log('Frontend - received API response:', response);
          this.updateActivityChartForPeriod(response);
        },
        error: (error) => {
          console.error('Error loading activity for period:', error);
        }
      });
  }

  private updateCharts(): void {
    // Charts are updated via individual update methods when API data arrives
    // No longer reinitializing with hardcoded data
  }

  /**
   * Date picker change handler
   */
  onDateChange(): void {
    this.loadDashboardData();
  }

  /**
   * Date picker selection handler
   */
  onDateSelect(date: NgbDateStruct): void {
    console.log('Date selected:', date);
    this.currentDate = date;
    this.loadDashboardData();
  }

  /**
   * Update activity chart for different periods
   */
  private updateActivityChartForPeriod(response: any): void {
    const activityData = response.data || response;
    const labels = response.labels || activityData.labels || [];
    const data = response.data || activityData.data || activityData;

    console.log('Frontend - updateActivityChartForPeriod called with:', response);
    console.log('Frontend - activityData:', activityData);
    console.log('Frontend - response.period:', response.period);
    console.log('Frontend - labels extracted:', labels);
    console.log('Frontend - data extracted:', data);

    if (response.period === 'day' || response.period === 'week' || response.period === 'month') {
      console.log('Frontend - Setting categories to:', labels);

      // Force chart to re-render by creating completely new options object
      this.activityChartOptions = {
        series: [{
          name: "Guest Interactions",
          data: data
        }],
        chart: {
          type: "area",
          height: '350',
          parentHeightOffset: 0,
          foreColor: this.themeCssVariables.secondary,
          toolbar: {
            show: false
          },
          zoom: {
            enabled: false
          },
          id: 'activity-chart-' + Date.now() // Force chart re-creation
        },
        xaxis: {
          type: 'category',
          categories: labels,
          lines: {
            show: true
          },
          axisBorder: {
            color: this.themeCssVariables.gridBorder,
          },
          axisTicks: {
            color: this.themeCssVariables.gridBorder,
          },
          labels: {
            style: {
              colors: this.themeCssVariables.secondary,
              fontSize: '12px'
            }
          }
        },
        colors: ['#25D466'], // Green color
        fill: {
          type: 'gradient',
          gradient: {
            shade: 'light',
            type: 'vertical',
            shadeIntensity: 0.5,
            gradientToColors: ['#E8FAF0'],
            inverseColors: false,
            opacityFrom: 0.8,
            opacityTo: 0.1,
          }
        },
        grid: {
          padding: {
            bottom: -4,
          },
          borderColor: this.themeCssVariables.gridBorder,
          xaxis: {
            lines: {
              show: true
            }
          }
        },
        yaxis: {
          title: {
            text: 'Interactions',
            style: {
              size: 9,
              color: this.themeCssVariables.secondary
            }
          },
          tickAmount: 4,
        },
        stroke: {
          width: 2,
          curve: "smooth",
        },
        noData: {
          text: data && data.some && data.some((v: number) => v > 0) ? undefined : 'No activity data available'
        }
      };
    } else {
      // Daily view - use existing method with proper data format
      const dailyData = Array.isArray(data) ? data : (activityData.data || activityData);
      console.log('Frontend - Daily view, using data:', dailyData);
      this.updateActivityChart(dailyData);
    }
  }

  /**
   * Update activity chart with real data
   */
  private updateActivityChart(hourlyData: number[]): void {
    // Check if there's any actual activity data
    const hasData = hourlyData && hourlyData.some(value => value > 0);

    // Create chart options for daily view (hourly format)
    this.activityChartOptions = {
      series: [{
        name: "Guest Interactions",
        data: hasData ? hourlyData : new Array(24).fill(0)
      }],
      chart: {
        type: "area",
        height: '350',
        parentHeightOffset: 0,
        foreColor: this.themeCssVariables.secondary,
        toolbar: {
          show: false
        },
        zoom: {
          enabled: false
        }
      },
      colors: ['#25D466'], // Green color
      fill: {
        type: 'gradient',
        gradient: {
          shade: 'light',
          type: 'vertical',
          shadeIntensity: 0.5,
          gradientToColors: ['#E8FAF0'],
          inverseColors: false,
          opacityFrom: 0.8,
          opacityTo: 0.1,
        }
      },
      grid: {
        padding: {
          bottom: -4,
        },
        borderColor: this.themeCssVariables.gridBorder,
        xaxis: {
          lines: {
            show: true
          }
        }
      },
      xaxis: {
        categories: Array.from({length: 24}, (_, i) => `${i.toString().padStart(2, '0')}:00`),
        type: 'category',
        axisBorder: {
          color: this.themeCssVariables.gridBorder,
        },
        axisTicks: {
          color: this.themeCssVariables.gridBorder,
        },
        labels: {
          style: {
            colors: this.themeCssVariables.secondary,
            fontSize: '12px'
          }
        }
      },
      yaxis: {
        title: {
          text: 'Interactions',
          style: {
            size: 9,
            color: this.themeCssVariables.secondary
          }
        },
        tickAmount: 4,
      },
      stroke: {
        width: 2,
        curve: "smooth",
      },
      noData: {
        text: hasData ? undefined : 'No activity data available for this date'
      }
    };
  }

  /**
   * Update department chart with real data
   */
  private updateDepartmentChart(): void {
    // Ensure chart options are initialized before updating
    if (!this.departmentChartOptions) {
      this.departmentChartOptions = this.getDepartmentTasksChartOptions(this.themeCssVariables);
    }

    if (this.departmentTasks.length > 0) {
      const series = this.departmentTasks.map(dept => dept.count || 0);
      const labels = this.departmentTasks.map(dept => dept.department || 'Unknown');
      const colors = this.departmentTasks.map(dept => dept.color || '#6C757D');

      this.departmentChartOptions = {
        ...this.departmentChartOptions,
        series: series,
        labels: labels,
        colors: colors
      };
    } else {
      // Show empty state
      this.departmentChartOptions = {
        ...this.departmentChartOptions,
        series: [1],
        labels: ['No Data Available'],
        colors: ['#E0E0E0']
      };
    }
  }

  /**
   * Update task trend chart with real data
   */
  private updateTaskTrendChart(trendData: { completed: number[], created: number[] }): void {
    // Ensure chart options are initialized before updating
    if (!this.taskTrendChartOptions) {
      this.taskTrendChartOptions = this.getTaskCompletionChartOptions(this.themeCssVariables);
    }

    const dayLabels = Array.from({ length: 7 }, (_, i) => {
      const date = new Date();
      date.setDate(date.getDate() - (6 - i));
      return date.toLocaleDateString('en-US', { weekday: 'short' });
    });

    const hasData = trendData &&
      ((trendData.completed && trendData.completed.some(v => v > 0)) ||
       (trendData.created && trendData.created.some(v => v > 0)));

    if (hasData) {
      this.taskTrendChartOptions = {
        ...this.taskTrendChartOptions,
        series: [
          {
            name: 'Completed',
            data: trendData.completed || new Array(7).fill(0)
          },
          {
            name: 'Created',
            data: trendData.created || new Array(7).fill(0)
          }
        ],
        xaxis: {
          ...(this.taskTrendChartOptions.xaxis || {}),
          categories: dayLabels
        },
        noData: {
          text: undefined
        }
      };
    } else {
      this.taskTrendChartOptions = {
        ...this.taskTrendChartOptions,
        series: [
          {
            name: 'Completed',
            data: new Array(7).fill(0)
          },
          {
            name: 'Created',
            data: new Array(7).fill(0)
          }
        ],
        xaxis: {
          ...(this.taskTrendChartOptions.xaxis || {}),
          categories: dayLabels
        },
        noData: {
          text: 'No task completion data available for the selected period'
        }
      };
    }
  }



  /**
   * Active Guests chart options
   */
  getGuestsChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: 'Active Guests',
        data: [35, 38, 42, 45, 41, 39, 42, 44, 40, 38, 42]
      }],
      chart: {
        type: "line",
        height: 60,
        sparkline: {
          enabled: true
        }
      },
      colors: ['#25D466'], // Green color
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024"],
      },
      stroke: {
        width: 2,
        curve: "smooth"
      },
      markers: {
        size: 0
      },
    };
  }

  /**
   * Tasks chart options
   */
  getTasksChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: 'Tasks',
        data: [15, 22, 18, 25, 20, 17, 18, 16, 19, 14, 18]
      }],
      chart: {
        type: "bar",
        height: 60,
        sparkline: {
          enabled: true
        }
      },
      colors: ['#25D466'], // Green color
      plotOptions: {
        bar: {
          borderRadius: 2,
          columnWidth: "60%"
        }
      },
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024"],
      }
    };
  }

  /**
   * Response Time chart options
   */
  getResponseTimeChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: 'Response Time (min)',
        data: [8, 6, 9, 7, 5, 8, 6, 9, 7, 8, 6]
      }],
      chart: {
        type: "line",
        height: 60,
        sparkline: {
          enabled: true
        }
      },
      colors: ['#25D466'], // Green color
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024"],
      },
      stroke: {
        width: 2,
        curve: "smooth"
      },
      markers: {
        size: 0
      },
    };
  }

  /**
   * Daily Activity chart options
   */
  getDailyActivityChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: "Guest Interactions",
        data: new Array(24).fill(0)
      }],
      chart: {
        type: "area",
        height: '350',
        parentHeightOffset: 0,
        foreColor: themeVariables.secondary,
        toolbar: {
          show: false
        },
        zoom: {
          enabled: false
        }
      },
      colors: ['#25D466'], // Green color
      fill: {
        type: 'gradient',
        gradient: {
          shade: 'light',
          type: 'vertical',
          shadeIntensity: 0.5,
          gradientToColors: ['#E8FAF0'],
          inverseColors: false,
          opacityFrom: 0.8,
          opacityTo: 0.1,
        }
      },
      grid: {
        padding: {
          bottom: -4,
        },
        borderColor: themeVariables.gridBorder,
        xaxis: {
          lines: {
            show: true
          }
        }
      },
      xaxis: {
        categories: Array.from({length: 24}, (_, i) => `${i.toString().padStart(2, '0')}:00`),
        lines: {
          show: true
        },
        axisBorder: {
          color: themeVariables.gridBorder,
        },
        axisTicks: {
          color: themeVariables.gridBorder,
        },
      },
      yaxis: {
        title: {
          text: 'Interactions',
          style: {
            size: 9,
            color: themeVariables.secondary
          }
        },
        tickAmount: 4,
      },
      stroke: {
        width: 2,
        curve: "smooth",
      },
      noData: {
        text: 'No activity data available'
      }
    };
  }

  /**
   * Department Tasks chart options
   */
  getDepartmentTasksChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [8, 5, 3, 2],
      chart: {
        height: 280,
        type: "donut"
      },
      colors: ['#25D466', '#FFC107', '#0DCAF0', '#6C757D'],
      plotOptions: {
        pie: {
          donut: {
            size: '70%'
          }
        }
      },
      labels: ['Housekeeping', 'Maintenance', 'Front Desk', 'Concierge'],
      legend: {
        show: true,
        position: "bottom",
        horizontalAlign: 'center',
        fontFamily: themeVariables.fontFamily,
      },
      dataLabels: {
        enabled: true,
        formatter: function (val: any) {
          return Math.round(val) + "%"
        }
      },
      responsive: [{
        breakpoint: 480,
        options: {
          chart: {
            height: 200
          },
          legend: {
            position: 'bottom'
          }
        }
      }]
    };
  }

  /**
   * Task Completion chart options
   */
  getTaskCompletionChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: 'Completed',
        data: [12, 15, 18, 14, 20, 16, 22]
      }, {
        name: 'Created',
        data: [15, 18, 16, 17, 22, 19, 18]
      }],
      chart: {
        type: 'bar',
        height: '330',
        parentHeightOffset: 0,
        foreColor: themeVariables.secondary,
        toolbar: {
          show: false
        }
      },
      colors: ['#25D466', '#E8FAF0'],
      fill: {
        opacity: 0.9
      },
      grid: {
        padding: {
          bottom: -4
        },
        borderColor: themeVariables.gridBorder,
      },
      xaxis: {
        categories: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
        axisBorder: {
          color: themeVariables.gridBorder,
        },
        axisTicks: {
          color: themeVariables.gridBorder,
        },
      },
      yaxis: {
        title: {
          text: 'Number of Tasks',
          style: {
            size: 9,
            color: themeVariables.secondary
          }
        },
      },
      legend: {
        show: true,
        position: "top",
        horizontalAlign: 'center',
        fontFamily: themeVariables.fontFamily,
      },
      dataLabels: {
        enabled: false
      },
      plotOptions: {
        bar: {
          columnWidth: "60%",
          borderRadius: 4,
        },
      }
    };
  }

  /**
   * Default Activity Chart options with empty data
   */
  getDefaultActivityChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: "Guest Interactions",
        data: new Array(24).fill(0)
      }],
      chart: {
        type: 'line',
        height: 400,
        parentHeightOffset: 0,
        foreColor: themeVariables.secondary,
        toolbar: {
          show: false
        }
      },
      colors: ['#25D466'],
      stroke: {
        width: 2,
        curve: 'smooth'
      },
      grid: {
        borderColor: themeVariables.gridBorder,
        padding: {
          bottom: -6
        }
      },
      xaxis: {
        categories: Array.from({length: 24}, (_, i) => `${i.toString().padStart(2, '0')}:00`),
        axisBorder: {
          color: themeVariables.gridBorder,
        },
        axisTicks: {
          color: themeVariables.gridBorder,
        },
      },
      yaxis: {
        title: {
          text: 'Interactions',
          style: {
            size: 9,
            color: themeVariables.secondary
          }
        },
      },
      markers: {
        size: 0
      },
      dataLabels: {
        enabled: false
      },
      tooltip: {
        enabled: true,
        theme: 'dark'
      }
    };
  }

  /**
   * Legacy chart method - kept for compatibility
   */
  getCustomersChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: '',
        data: [3844, 3855, 3841, 3867, 3822, 3843, 3821, 3841, 3856, 3827, 3843]
      }],
      chart: {
        type: "line",
        height: 60,
        sparkline: {
          enabled: !0
        }
      },
      colors: [themeVariables.primary],
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024",],
      },
      stroke: {
        width: 2,
        curve: "smooth"
      },
      markers: {
        size: 0
      },
    }
  };



  /**
   * Orders chart options
   */
  getOrdersChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: '',
        data: [36, 77, 52, 90, 74, 35, 55, 23, 47, 10, 63]
      }],
      chart: {
        type: "bar",
        height: 60,
        sparkline: {
          enabled: !0
        }
      },
      colors: [themeVariables.primary],
      plotOptions: {
        bar: {
          borderRadius: 2,
          columnWidth: "60%"
        }
      },
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024",],
      }
    }
  };



  /**
   * Growth chart options
   */
  getGrowthChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: '',
        data: [41, 45, 44, 46, 52, 54, 43, 74, 82, 82, 89]
      }],
      chart: {
        type: "line",
        height: 60,
        sparkline: {
          enabled: !0
        }
      },
      colors: [themeVariables.primary],
      xaxis: {
        type: 'datetime',
        categories: ["Jan 01 2024", "Jan 02 2024", "Jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024",],
      },
      stroke: {
        width: 2,
        curve: "smooth"
      },
      markers: {
        size: 0
      },
    }
  };



  /**
   * Revenue chart options
   */
  getRevenueChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: "Revenue",
        data: [
          49.3,
          48.7,
          50.6,
          53.3,
          54.7,
          53.8,
          54.6,
          56.7,
          56.9,
          56.1,
          56.5,
          60.3,
          58.7,
          61.4,
          61.1,
          58.5,
          54.7,
          52.0,
          51.0,
          47.4,
          48.5,
          48.9,
          53.5,
          50.2,
          46.2,
          48.6,
          51.7,
          51.3,
          50.2,
          54.6,
          52.4,
          53.0,
          57.0,
          52.9,
          48.7,
          52.6,
          53.5,
          58.5,
          55.1,
          58.0,
          61.3,
          57.7,
          60.2,
          61.0,
          57.7,
          56.8,
          58.9,
          62.4,
          58.7,
          58.4,
          56.7,
          52.7,
          52.3,
          50.5,
          55.4,
          50.4,
          52.4,
          48.7,
          47.4,
          43.3,
          38.9,
          34.7,
          31.0,
          32.6,
          36.8,
          35.8,
          32.7,
          33.2,
          30.8,
          28.6,
          28.4,
          27.7,
          27.7,
          25.9,
          24.3,
          21.9,
          22.0,
          23.5,
          27.3,
          30.2,
          27.2,
          29.9,
          25.1,
          23.0,
          23.7,
          23.4,
          27.9,
          23.2,
          23.9,
          19.2,
          15.1,
          15.0,
          11.0,
          9.20,
          7.47,
          11.6,
          15.7,
          13.9,
          12.5,
          13.5,
          15.0,
          13.9,
          13.2,
          18.1,
          20.6,
          21.0,
          25.3,
          25.3,
          20.9,
          18.7,
          15.3,
          14.5,
          17.9,
          15.9,
          16.3,
          14.1,
          12.1,
          14.8,
          17.2,
          17.7,
          14.0,
          18.6,
          18.4,
          22.6,
          25.0,
          28.1,
          28.0,
          24.1,
          24.2,
          28.2,
          26.2,
          29.3,
          26.0,
          23.9,
          28.8,
          25.1,
          21.7,
          23.0,
          20.7,
          29.7,
          30.2,
          32.5,
          31.4,
          33.6,
          30.0,
          34.2,
          36.9,
          35.5,
          34.7,
          36.9
        ]
      }],
      chart: {
        type: "line",
        height: '400',
        parentHeightOffset: 0,
        foreColor: themeVariables.secondary,
        toolbar: {
          show: false
        },
        zoom: {
          enabled: false
        }
      },
      colors: [themeVariables.primary, themeVariables.danger, themeVariables.warning],
      grid: {
        padding: {
          bottom: -4,
        },
        borderColor: themeVariables.gridBorder,
        xaxis: {
          lines: {
            show: true
          }
        }
      },
      xaxis: {
        type: "datetime",
        categories: [
          "Jan 01 2024", "Jan 02 2024", "jan 03 2024", "Jan 04 2024", "Jan 05 2024", "Jan 06 2024", "Jan 07 2024", "Jan 08 2024", "Jan 09 2024", "Jan 10 2024", "Jan 11 2024", "Jan 12 2024", "Jan 13 2024", "Jan 14 2024", "Jan 15 2024", "Jan 16 2024", "Jan 17 2024", "Jan 18 2024", "Jan 19 2024", "Jan 20 2024","Jan 21 2024", "Jan 22 2024", "Jan 23 2024", "Jan 24 2024", "Jan 25 2024", "Jan 26 2024", "Jan 27 2024", "Jan 28 2024", "Jan 29 2024", "Jan 30 2024", "Jan 31 2024",
          "Feb 01 2024", "Feb 02 2024", "Feb 03 2024", "Feb 04 2024", "Feb 05 2024", "Feb 06 2024", "Feb 07 2024", "Feb 08 2024", "Feb 09 2024", "Feb 10 2024", "Feb 11 2024", "Feb 12 2024", "Feb 13 2024", "Feb 14 2024", "Feb 15 2024", "Feb 16 2024", "Feb 17 2024", "Feb 18 2024", "Feb 19 2024", "Feb 20 2024","Feb 21 2024", "Feb 22 2024", "Feb 23 2024", "Feb 24 2024", "Feb 25 2024", "Feb 26 2024", "Feb 27 2024", "Feb 28 2024",
          "Mar 01 2024", "Mar 02 2024", "Mar 03 2024", "Mar 04 2024", "Mar 05 2024", "Mar 06 2024", "Mar 07 2024", "Mar 08 2024", "Mar 09 2024", "Mar 10 2024", "Mar 11 2024", "Mar 12 2024", "Mar 13 2024", "Mar 14 2024", "Mar 15 2024", "Mar 16 2024", "Mar 17 2024", "Mar 18 2024", "Mar 19 2024", "Mar 20 2024","Mar 21 2024", "Mar 22 2024", "Mar 23 2024", "Mar 24 2024", "Mar 25 2024", "Mar 26 2024", "Mar 27 2024", "Mar 28 2024", "Mar 29 2024", "Mar 30 2024", "Mar 31 2024",
          "Apr 01 2024", "Apr 02 2024", "Apr 03 2024", "Apr 04 2024", "Apr 05 2024", "Apr 06 2024", "Apr 07 2024", "Apr 08 2024", "Apr 09 2024", "Apr 10 2024", "Apr 11 2024", "Apr 12 2024", "Apr 13 2024", "Apr 14 2024", "Apr 15 2024", "Apr 16 2024", "Apr 17 2024", "Apr 18 2024", "Apr 19 2024", "Apr 20 2024","Apr 21 2024", "Apr 22 2024", "Apr 23 2024", "Apr 24 2024", "Apr 25 2024", "Apr 26 2024", "Apr 27 2024", "Apr 28 2024", "Apr 29 2024", "Apr 30 2024",
          "May 01 2024", "May 02 2024", "May 03 2024", "May 04 2024", "May 05 2024", "May 06 2024", "May 07 2024", "May 08 2024", "May 09 2024", "May 10 2024", "May 11 2024", "May 12 2024", "May 13 2024", "May 14 2024", "May 15 2024", "May 16 2024", "May 17 2024", "May 18 2024", "May 19 2024", "May 20 2024","May 21 2024", "May 22 2024", "May 23 2024", "May 24 2024", "May 25 2024", "May 26 2024", "May 27 2024", "May 28 2024", "May 29 2024", "May 30 2024",
        ],
        lines: {
          show: true
        },
        axisBorder: {
          color: themeVariables.gridBorder,
        },
        axisTicks: {
          color: themeVariables.gridBorder,
        },
        crosshairs: {
          stroke: {
            color: themeVariables.secondary,
          },
        },
      },
      yaxis: {
        title: {
          text: 'Revenue ( $1000 x )',
          style:{
            size: 9,
            color: themeVariables.secondary
          }
        },
        tickAmount: 4,
        tooltip: {
          enabled: true
        },
        crosshairs: {
          stroke: {
            color: themeVariables.secondary,
          },
        },
        labels: {
          offsetX: 0,
        },
      },
      markers: {
        size: 0,
      },
      stroke: {
        width: 2,
        curve: "straight",
      },
    }
  };



  /**
   * Monthly sales chart options
   */
  getMonthlySalesChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [{
        name: 'Sales',
        data: [152,109,93,113,126,161,188,143,102,113,116,124]
      }],
      chart: {
        type: 'bar',
        height: '330',
        parentHeightOffset: 0,
        foreColor: themeVariables.secondary,
        toolbar: {
          show: false
        },
        zoom: {
          enabled: false
        }
      },
      colors: [themeVariables.primary],  
      fill: {
        opacity: .9
      } , 
      grid: {
        padding: {
          bottom: -4
        },
        borderColor: themeVariables.gridBorder,
        xaxis: {
          lines: {
            show: true
          }
        }
      },
      xaxis: {
        type: 'datetime',
        categories: ['01/01/2024','02/01/2024','03/01/2024','04/01/2024','05/01/2024','06/01/2024','07/01/2024', '08/01/2024','09/01/2024','10/01/2024', '11/01/2024', '12/01/2024'],
        axisBorder: {
          color: themeVariables.gridBorder,
        },
        axisTicks: {
          color: themeVariables.gridBorder,
        },
      },
      yaxis: {
        title: {
          text: 'Number of Sales',
          style:{
            size: 9,
            color: themeVariables.secondary
          }
        },
        labels: {
          offsetX: 0,
        },
      },
      legend: {
        show: true,
        position: "top",
        horizontalAlign: 'center',
        fontFamily: themeVariables.fontFamily,
        itemMargin: {
          horizontal: 8,
          vertical: 0
        },
      },
      stroke: {
        width: 0
      },
      dataLabels: {
        enabled: true,
        style: {
          fontSize: '10px',
          fontFamily: themeVariables.fontFamily,
        },
        offsetY: -27
      },
      plotOptions: {
        bar: {
          columnWidth: "50%",
          borderRadius: 4,
          dataLabels: {
            position: 'top',
            orientation: 'vertical',
          }
        },
      }
    }
  }



  /**
   * Cloud storage chart options
   */
  getCloudStorageChartOptions(themeVariables: ThemeCssVariablesType) {
    return {
      series: [67],
      chart: {
        height: 260,
        type: "radialBar"
      },
      colors: [themeVariables.primary],
      plotOptions: {
        radialBar: {
          hollow: {
            margin: 15,
            size: "70%"
          },
          track: {
            show: true,
            background: themeVariables.gridBorder,
            strokeWidth: '100%',
            opacity: 1,
            margin: 5, 
          },
          dataLabels: {
            showOn: "always",
            name: {
              offsetY: -11,
              show: true,
              color: themeVariables.secondary,
              fontSize: "13px"
            },
            value: {
              color: themeVariables.secondary,
              fontSize: "30px",
              show: true
            }
          }
        }
      },
      fill: {
        opacity: 1
      },
      stroke: {
        lineCap: "round",
      },
      labels: ["Storage Used"]
    }
  };
  
}
