import { Component, Input } from '@angular/core';
import { NgClass, CommonModule } from '@angular/common';
import { FeatherIconDirective } from '../../../core/feather-icon/feather-icon.directive';

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [CommonModule, NgClass, FeatherIconDirective],
  template: `
    <div class="card">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-start">
          <div>
            <h6 class="card-title mb-2 text-muted">{{ title }}</h6>
            <h2 class="mb-0" [ngClass]="valueClass">{{ value }}</h2>
            <p class="text-muted mb-0 mt-2">
              <span [ngClass]="trendClass" *ngIf="trend">
                <i [appFeatherIcon]="trendIcon"></i>
                {{ trendValue }}{{ trendSuffix }}
              </span>
              <span class="ms-1" *ngIf="subtitle">{{ subtitle }}</span>
            </p>
          </div>
          <div class="stats-icon">
            <i [appFeatherIcon]="icon" [ngClass]="iconClass"></i>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .card {
      border: none;
      box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
      transition: all 0.2s ease-in-out;
    }
    .card:hover {
      box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.1);
      transform: translateY(-2px);
    }
    .stats-icon {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 24px;
    }
    .stats-icon.primary {
      background-color: rgba(37, 212, 102, 0.1);
      color: #25D466;
    }
    .stats-icon.success {
      background-color: rgba(37, 212, 102, 0.1);
      color: #25D466;
    }
    .stats-icon.warning {
      background-color: rgba(255, 193, 7, 0.1);
      color: #ffc107;
    }
    .stats-icon.danger {
      background-color: rgba(220, 53, 69, 0.1);
      color: #dc3545;
    }
    .stats-icon.info {
      background-color: rgba(13, 202, 240, 0.1);
      color: #0dcaf0;
    }
    .text-trend-up {
      color: #25D466 !important;
    }
    .text-trend-down {
      color: #dc3545 !important;
    }
    .text-trend-neutral {
      color: #6c757d !important;
    }
  `]
})
export class StatsCardComponent {
  @Input() title: string = '';
  @Input() value: string | number = '';
  @Input() icon: string = 'activity';
  @Input() iconClass: string = 'primary';
  @Input() valueClass: string = '';
  @Input() trend?: 'up' | 'down' | 'neutral';
  @Input() trendValue?: string | number;
  @Input() trendSuffix?: string = '';
  @Input() subtitle?: string;

  get trendIcon(): string {
    switch (this.trend) {
      case 'up': return 'trending-up';
      case 'down': return 'trending-down';
      default: return 'minus';
    }
  }

  get trendClass(): string {
    switch (this.trend) {
      case 'up': return 'text-trend-up';
      case 'down': return 'text-trend-down';
      default: return 'text-trend-neutral';
    }
  }
}