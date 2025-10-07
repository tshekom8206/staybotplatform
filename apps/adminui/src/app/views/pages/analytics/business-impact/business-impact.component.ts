import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgbDropdownModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ApexOptions, NgApexchartsModule } from 'ng-apexcharts';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { AnalyticsService } from '../../../../core/services/analytics.service';

@Component({
  selector: 'app-business-impact',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    NgApexchartsModule,
    FeatherIconDirective
  ],
  templateUrl: './business-impact.component.html',
  styleUrl: './business-impact.component.scss'
})
export class BusinessImpactComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private analyticsService = inject(AnalyticsService);

  // Make Math available in template
  Math = Math;

  // Loading states
  loading = true;

  // Data from 6 endpoints
  hotelPerformance: any = null;
  operationalPerformance: any = null;
  satisfactionTrends: any = null;
  revenueInsights: any = null;
  upsellingRoi: any = null;
  immediateActions: any = null;

  // Filter settings
  selectedPeriod: 'week' | 'month' | 'quarter' | 'year' = 'month';

  // Chart options
  satisfactionTrendChart: ApexOptions = {};
  operationalChart: ApexOptions = {};
  revenueSegmentChart: ApexOptions = {};

  ngOnInit(): void {
    this.loadDashboardData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboardData(): void {
    this.loading = true;

    forkJoin({
      hotel: this.analyticsService.getHotelPerformance(),
      operational: this.analyticsService.getOperationalPerformance(),
      satisfaction: this.analyticsService.getGuestSatisfactionTrends(),
      revenue: this.analyticsService.getRevenueInsights(),
      upselling: this.analyticsService.getUpsellingRoi(),
      actions: this.analyticsService.getImmediateActions()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (data) => {
        this.hotelPerformance = data.hotel;
        this.operationalPerformance = data.operational;
        this.satisfactionTrends = data.satisfaction;
        this.revenueInsights = data.revenue;
        this.upsellingRoi = data.upselling;
        this.immediateActions = data.actions?.data || data.actions;

        console.log('Upselling ROI Data:', this.upsellingRoi);
        console.log('Immediate Actions Data:', this.immediateActions);

        this.initializeCharts();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading business impact data:', error);
        this.loading = false;
      }
    });
  }

  refresh(): void {
    this.loadDashboardData();
  }

  private initializeCharts(): void {
    this.initializeSatisfactionTrendChart();
    this.initializeOperationalChart();
    this.initializeRevenueSegmentChart();
  }

  private initializeSatisfactionTrendChart(): void {
    if (!this.satisfactionTrends?.timeSeries?.length) return;

    const categories = this.satisfactionTrends.timeSeries.map((t: any) => t.date);
    const scores = this.satisfactionTrends.timeSeries.map((t: any) => t.score);

    this.satisfactionTrendChart = {
      series: [{
        name: 'Satisfaction Score',
        data: scores
      }],
      chart: {
        type: 'area',
        height: 300,
        toolbar: { show: false }
      },
      colors: ['#25D466'],
      fill: {
        type: 'gradient',
        gradient: {
          opacityFrom: 0.6,
          opacityTo: 0.1
        }
      },
      stroke: {
        width: 2,
        curve: 'smooth'
      },
      xaxis: {
        categories: categories,
        labels: {
          rotate: -45
        }
      },
      yaxis: {
        min: 0,
        max: 5,
        title: {
          text: 'Score (1-5)'
        }
      },
      dataLabels: {
        enabled: false
      }
    };
  }

  private initializeOperationalChart(): void {
    if (!this.operationalPerformance?.departments?.length) return;

    const categories = this.operationalPerformance.departments.map((d: any) => d.name);
    const completionRates = this.operationalPerformance.departments.map((d: any) => d.completionRate);

    this.operationalChart = {
      series: [{
        name: 'Completion Rate',
        data: completionRates
      }],
      chart: {
        type: 'bar',
        height: 300,
        toolbar: { show: false }
      },
      colors: ['#17a2b8'],
      plotOptions: {
        bar: {
          horizontal: true,
          dataLabels: {
            position: 'top'
          }
        }
      },
      dataLabels: {
        enabled: true,
        formatter: (val: number) => val.toFixed(1) + '%',
        offsetX: 40
      },
      xaxis: {
        categories: categories,
        max: 100
      }
    };
  }

  private initializeRevenueSegmentChart(): void {
    if (!this.revenueInsights?.satisfactionRevenueCorrelation?.segments?.length) return;

    const segments = this.revenueInsights.satisfactionRevenueCorrelation.segments;
    const categories = segments.map((s: any) => s.satisfactionLevel);
    const revenue = segments.map((s: any) => s.totalRevenue);

    this.revenueSegmentChart = {
      series: [{
        name: 'Total Revenue',
        data: revenue
      }],
      chart: {
        type: 'bar',
        height: 250,
        toolbar: { show: false }
      },
      colors: ['#25D466', '#FFC107', '#FD7E14', '#DC3545'],
      plotOptions: {
        bar: {
          distributed: true,
          borderRadius: 4
        }
      },
      dataLabels: {
        enabled: true,
        formatter: (val: number) => 'R' + val.toLocaleString('en-ZA')
      },
      xaxis: {
        categories: categories
      },
      legend: {
        show: false
      }
    };
  }

  exportPdfReport(): void {
    this.analyticsService.exportBusinessImpactReport().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `Business_Impact_Report_${new Date().toISOString().split('T')[0]}.pdf`;
        link.click();
        window.URL.revokeObjectURL(url);
      },
      error: (error) => {
        console.error('Error exporting report:', error);
        alert('Failed to export report. Please try again.');
      }
    });
  }

  // Utility methods
  getPerformanceClass(value: number, type: 'occupancy' | 'satisfaction' | 'nps' | 'completion'): string {
    switch (type) {
      case 'occupancy':
        if (value >= 90) return 'text-success';
        if (value >= 70) return 'text-warning';
        return 'text-danger';

      case 'satisfaction':
        if (value >= 4.5) return 'text-success';
        if (value >= 4.0) return 'text-info';
        if (value >= 3.5) return 'text-warning';
        return 'text-danger';

      case 'nps':
        if (value >= 50) return 'text-success';
        if (value >= 0) return 'text-warning';
        return 'text-danger';

      case 'completion':
        if (value >= 90) return 'text-success';
        if (value >= 70) return 'text-warning';
        return 'text-danger';

      default:
        return 'text-secondary';
    }
  }

  getTrendIcon(trend: string): string {
    return trend === 'up' || trend === 'improving' ? 'trending-up' :
           trend === 'down' || trend === 'declining' ? 'trending-down' : 'minus';
  }

  getTrendClass(trend: string): string {
    return trend === 'up' || trend === 'improving' ? 'text-success' :
           trend === 'down' || trend === 'declining' ? 'text-danger' : 'text-secondary';
  }

  formatCurrency(value: number): string {
    return 'R' + (value || 0).toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  formatPercentage(value: number): string {
    return (value || 0).toFixed(1) + '%';
  }

  getPriorityBadgeClass(priority: string): string {
    switch (priority?.toLowerCase()) {
      case 'high':
      case 'critical':
        return 'bg-danger bg-opacity-10 text-danger';
      case 'medium':
        return 'bg-warning bg-opacity-10 text-warning';
      case 'low':
        return 'bg-info bg-opacity-10 text-info';
      default:
        return 'bg-secondary bg-opacity-10 text-secondary';
    }
  }

  getDepartmentPerformanceClass(performance: string): string {
    switch (performance?.toLowerCase()) {
      case 'excellent':
        return 'badge-success';
      case 'good':
        return 'badge-info';
      case 'fair':
        return 'badge-warning';
      case 'needs_attention':
        return 'badge-danger';
      default:
        return 'badge-secondary';
    }
  }

  // Generate strategic recommendations based on all dashboard data
  getStrategicRecommendations(): any[] {
    const recommendations = [];

    // 1. Revenue Opportunity Recommendation
    if (this.revenueInsights?.roiInsights?.improvementPotential > 0) {
      recommendations.push({
        priority: 'high',
        category: 'Revenue Growth',
        title: 'Unlock Revenue Potential Through Guest Satisfaction',
        insight: `${this.formatCurrency(this.revenueInsights.roiInsights.improvementPotential)} in additional revenue is achievable by improving guest satisfaction scores.`,
        action: 'Focus on converting Fair and Poor satisfaction guests (below 4.0) to Good+ satisfaction (4.0+). Target the "Fair" and "Poor" segments identified in Guest Segmentation Analysis.',
        impact: 'High',
        timeframe: '30-60 days',
        icon: 'trending-up'
      });
    }

    // 2. Operational Efficiency Recommendation
    const lowPerformingDepts = this.operationalPerformance?.departments?.filter((d: any) => d.completionRate < 70) || [];
    if (lowPerformingDepts.length > 0) {
      recommendations.push({
        priority: 'high',
        category: 'Operational Excellence',
        title: 'Improve Department Completion Rates',
        insight: `${lowPerformingDepts.length} department(s) have completion rates below 70%: ${lowPerformingDepts.map((d: any) => d.name).join(', ')}.`,
        action: 'Reallocate staff resources during peak hours, implement task prioritization system, and provide additional training for underperforming teams.',
        impact: 'High',
        timeframe: '14-30 days',
        icon: 'users'
      });
    }

    // 3. Guest Satisfaction Trend Recommendation
    if (this.hotelPerformance?.guestSatisfaction < 4.0) {
      recommendations.push({
        priority: 'critical',
        category: 'Guest Experience',
        title: 'Critical: Guest Satisfaction Below Target',
        insight: `Current average satisfaction is ${this.hotelPerformance.guestSatisfaction.toFixed(1)}/5.0, below the industry benchmark of 4.0+.`,
        action: 'Conduct immediate guest feedback sessions, identify top 3 pain points, and implement quick-win improvements. Monitor daily satisfaction scores.',
        impact: 'Critical',
        timeframe: 'Immediate (0-7 days)',
        icon: 'alert-circle'
      });
    } else if (this.hotelPerformance?.guestSatisfaction >= 4.5) {
      recommendations.push({
        priority: 'medium',
        category: 'Guest Experience',
        title: 'Maintain Excellence & Build Loyalty',
        insight: `Outstanding satisfaction score of ${this.hotelPerformance.guestSatisfaction.toFixed(1)}/5.0 indicates excellent service delivery.`,
        action: 'Implement guest loyalty program, encourage online reviews, and document best practices to maintain service standards.',
        impact: 'Medium',
        timeframe: '30-90 days',
        icon: 'award'
      });
    }

    // 4. Occupancy Rate Recommendation
    if (this.hotelPerformance?.occupancyRate < 70) {
      recommendations.push({
        priority: 'high',
        category: 'Revenue Management',
        title: 'Boost Occupancy Rate',
        insight: `Current occupancy rate of ${this.formatPercentage(this.hotelPerformance.occupancyRate)} is below optimal levels (70%+).`,
        action: 'Launch targeted marketing campaigns, adjust pricing strategy for off-peak periods, and partner with travel agencies for group bookings.',
        impact: 'High',
        timeframe: '30-60 days',
        icon: 'home'
      });
    }

    // 5. NPS Improvement Recommendation
    if (this.hotelPerformance?.nps !== undefined && this.hotelPerformance.nps < 50) {
      recommendations.push({
        priority: 'high',
        category: 'Brand Reputation',
        title: 'Improve Net Promoter Score',
        insight: `NPS of ${this.hotelPerformance.nps} indicates limited guest advocacy. Target NPS should be 50+.`,
        action: 'Identify and resolve detractor complaints, create guest referral incentive program, and train staff on creating memorable experiences.',
        impact: 'High',
        timeframe: '60-90 days',
        icon: 'star'
      });
    }

    // 6. Critical Actions Follow-up
    const criticalActionsCount = this.immediateActions?.criticalCount || 0;
    if (criticalActionsCount > 0) {
      recommendations.push({
        priority: 'critical',
        category: 'Immediate Actions',
        title: 'Address Critical Issues Immediately',
        insight: `${criticalActionsCount} critical issue(s) require immediate management attention.`,
        action: 'Review all critical alerts in "Immediate Actions Required" panel. Assign ownership and set resolution deadlines within 24 hours.',
        impact: 'Critical',
        timeframe: 'Immediate (0-24 hours)',
        icon: 'alert-triangle'
      });
    }

    // Sort by priority: critical > high > medium > low
    const priorityOrder: any = { critical: 0, high: 1, medium: 2, low: 3 };
    return recommendations.sort((a, b) => priorityOrder[a.priority] - priorityOrder[b.priority]);
  }
}
