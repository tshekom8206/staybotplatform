import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import { AuthService } from '../../../../../core/services/auth.service';
import {
  BusinessRulesStats,
  UpsellAnalytics,
  AuditLogEntry
} from '../../models/business-rules.models';

@Component({
  selector: 'app-business-rules-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbTooltipModule,
    FeatherIconDirective
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class BusinessRulesDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);
  private authService = inject(AuthService);

  // Data properties
  stats: BusinessRulesStats | null = null;
  upsellAnalytics: UpsellAnalytics | null = null;
  recentChanges: AuditLogEntry[] = [];
  loading = true;
  error: string | null = null;

  // Tenant info
  tenantId = 1; // Will be retrieved from AuthService in production

  ngOnInit(): void {
    this.loadDashboardData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadDashboardData(): void {
    this.loading = true;
    this.error = null;

    // Load all dashboard data in parallel
    forkJoin({
      stats: this.businessRulesService.getBusinessRulesStats(this.tenantId),
      analytics: this.businessRulesService.getUpsellAnalytics(this.tenantId, 30),
      auditLog: this.businessRulesService.getAuditLog(this.tenantId, {
        sortBy: 'timestamp',
        sortDirection: 'desc'
      })
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.stats = data.stats;
          this.upsellAnalytics = data.analytics;
          this.recentChanges = data.auditLog.slice(0, 5); // Only show latest 5
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading dashboard data:', error);
          this.error = 'Failed to load dashboard data. Please try again.';
          this.loading = false;
        }
      });
  }

  // Utility methods
  getActiveRulePercentage(): number {
    if (!this.stats || this.stats.totalRules === 0) return 0;
    return Math.round((this.stats.activeRules / this.stats.totalRules) * 100);
  }

  getServicesWithRulesPercentage(): number {
    if (!this.stats || this.stats.totalServices === 0) return 0;
    return Math.round((this.stats.servicesWithRules / this.stats.totalServices) * 100);
  }

  getUpsellActivePercentage(): number {
    if (!this.stats || this.stats.totalUpsellItems === 0) return 0;
    return Math.round((this.stats.activeUpsellItems / this.stats.totalUpsellItems) * 100);
  }

  formatCurrency(cents: number): string {
    return `$${(cents / 100).toFixed(2)}`;
  }

  formatPercentage(value: number): string {
    return `${value.toFixed(1)}%`;
  }

  getActionIcon(action: string): string {
    switch (action) {
      case 'CREATE': return 'plus-circle';
      case 'UPDATE': return 'edit';
      case 'DELETE': return 'trash-2';
      case 'ACTIVATE': return 'check-circle';
      case 'DEACTIVATE': return 'x-circle';
      default: return 'activity';
    }
  }

  getActionClass(action: string): string {
    switch (action) {
      case 'CREATE': return 'text-success';
      case 'UPDATE': return 'text-info';
      case 'DELETE': return 'text-danger';
      case 'ACTIVATE': return 'text-success';
      case 'DEACTIVATE': return 'text-warning';
      default: return 'text-muted';
    }
  }

  getEntityTypeLabel(entityType: string): string {
    switch (entityType) {
      case 'ServiceBusinessRule': return 'Service Rule';
      case 'RequestItemRule': return 'Request Item Rule';
      case 'UpsellItem': return 'Upsell Item';
      default: return entityType;
    }
  }

  getTimeAgo(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(hours / 24);

    if (hours < 1) return 'Just now';
    if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
    if (days < 7) return `${days} day${days !== 1 ? 's' : ''} ago`;
    return date.toLocaleDateString();
  }

  refresh(): void {
    this.loadDashboardData();
  }
}
