import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import { UpsellItem, UpsellAnalytics } from '../../models/business-rules.models';

@Component({
  selector: 'app-upselling',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NgbNavModule,
    NgbTooltipModule,
    FeatherIconDirective
  ],
  templateUrl: './upselling.component.html',
  styleUrl: './upselling.component.scss'
})
export class UpsellingComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);

  activeTab = 1;
  upsellItems: UpsellItem[] = [];
  analytics: UpsellAnalytics | null = null;
  loading = true;
  error: string | null = null;
  tenantId = 1;

  ngOnInit(): void {
    this.loadUpsellData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadUpsellData(): void {
    this.loading = true;
    this.error = null;

    this.businessRulesService.getUpsellItems(this.tenantId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (items) => {
          this.upsellItems = items;
          this.loadAnalytics();
        },
        error: (error) => {
          console.error('Error loading upsell items:', error);
          this.error = 'Failed to load upsell data. Please try again.';
          this.loading = false;
        }
      });
  }

  private loadAnalytics(): void {
    this.businessRulesService.getUpsellAnalytics(this.tenantId, 30)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (analytics) => {
          this.analytics = analytics;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading analytics:', error);
          this.loading = false;
        }
      });
  }

  formatCurrency(cents: number): string {
    return `$${(cents / 100).toFixed(2)}`;
  }

  formatPercentage(value: number): string {
    return `${value.toFixed(1)}%`;
  }

  refresh(): void {
    this.loadUpsellData();
  }
}
