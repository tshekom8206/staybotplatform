import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, HouseRules } from '../../../core/services/guest-api.service';

interface RuleSection {
  key: string;
  icon: string;
  titleKey: string;
  content: string;
  gradient: string;
  lightBg: string;
}

@Component({
  selector: 'app-house-rules',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'houseRules.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'houseRules.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else {
          @if (ruleSections().length > 0) {
            <!-- Check-in/Check-out Times Card -->
            @if (rules()?.checkInTime || rules()?.checkOutTime) {
              <div class="times-card">
                <div class="times-grid">
                  @if (rules()?.checkInTime) {
                    <div class="time-item check-in">
                      <div class="time-icon">
                        <i class="bi bi-box-arrow-in-right"></i>
                      </div>
                      <div class="time-info">
                        <span class="time-label">{{ 'houseRules.checkIn' | translate }}</span>
                        <span class="time-value">{{ rules()!.checkInTime }}</span>
                      </div>
                    </div>
                  }
                  @if (rules()?.checkOutTime) {
                    <div class="time-item check-out">
                      <div class="time-icon">
                        <i class="bi bi-box-arrow-right"></i>
                      </div>
                      <div class="time-info">
                        <span class="time-label">{{ 'houseRules.checkOut' | translate }}</span>
                        <span class="time-value">{{ rules()!.checkOutTime }}</span>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }

            <!-- Rules Cards -->
            <div class="rules-grid">
              @for (rule of ruleSections(); track rule.key; let i = $index) {
                <div class="rule-card" [style.animation-delay]="(i * 0.08) + 's'">
                  <div class="rule-header" [style.background]="rule.gradient">
                    <div class="rule-icon-wrapper">
                      <i class="bi" [class]="rule.icon"></i>
                    </div>
                    <h3>{{ rule.titleKey | translate }}</h3>
                  </div>
                  <div class="rule-body" [style.background]="rule.lightBg">
                    <p>{{ rule.content }}</p>
                  </div>
                </div>
              }
            </div>
          } @else {
            <div class="no-rules">
              <div class="no-rules-icon">
                <i class="bi bi-clipboard-check"></i>
              </div>
              <p>{{ 'houseRules.noRules' | translate }}</p>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

    /* Page Header */
    .page-header {
      padding: 1.5rem 0 1.25rem;
      margin-bottom: 1rem;
    }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-decoration: none;
      font-size: 0.9rem;
      font-weight: 500;
      padding: 0.4rem 0.75rem;
      margin: -0.4rem -0.75rem 0.75rem;
      border-radius: 50px;
      transition: all 0.2s ease;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }
    .back-link:hover { background: rgba(255, 255, 255, 0.15); color: white; }
    .back-link i { font-size: 1rem; }
    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0 0 0.5rem;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }
    .page-subtitle {
      font-size: 0.95rem;
      color: rgba(255, 255, 255, 0.85);
      margin: 0;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .loading-spinner {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }

    /* Times Card - Prominent at top */
    .times-card {
      background: white;
      border-radius: 20px;
      padding: 1.25rem;
      margin-bottom: 1rem;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
    }

    .times-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
    }

    .time-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1rem;
      border-radius: 14px;
    }

    .time-item.check-in {
      background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
    }

    .time-item.check-out {
      background: linear-gradient(135deg, #fff3e0 0%, #ffe0b2 100%);
    }

    .time-icon {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
      flex-shrink: 0;
    }

    .check-in .time-icon {
      background: linear-gradient(135deg, #4caf50 0%, #2e7d32 100%);
      color: white;
    }

    .check-out .time-icon {
      background: linear-gradient(135deg, #ff9800 0%, #f57c00 100%);
      color: white;
    }

    .time-info {
      display: flex;
      flex-direction: column;
    }

    .time-label {
      font-size: 0.75rem;
      font-weight: 500;
      color: #666;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .time-value {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1a1a1a;
      line-height: 1.2;
    }

    /* Rules Grid */
    .rules-grid {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .rule-card {
      border-radius: 16px;
      overflow: hidden;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.06);
      animation: slideUp 0.4s ease forwards;
      opacity: 0;
      transform: translateY(20px);
    }

    @keyframes slideUp {
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .rule-header {
      padding: 1rem 1.25rem;
      display: flex;
      align-items: center;
      gap: 0.875rem;
      color: white;
    }

    .rule-icon-wrapper {
      width: 40px;
      height: 40px;
      background: rgba(255, 255, 255, 0.25);
      backdrop-filter: blur(10px);
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.2rem;
      flex-shrink: 0;
    }

    .rule-header h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 600;
      text-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
    }

    .rule-body {
      padding: 1.25rem;
    }

    .rule-body p {
      margin: 0;
      font-size: 0.9rem;
      color: #444;
      line-height: 1.65;
    }

    /* No Rules State */
    .no-rules {
      background: white;
      border-radius: 20px;
      text-align: center;
      padding: 3rem 1.5rem;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
    }

    .no-rules-icon {
      width: 80px;
      height: 80px;
      margin: 0 auto 1.25rem;
      background: linear-gradient(135deg, #f5f5f5 0%, #e0e0e0 100%);
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .no-rules-icon i {
      font-size: 2.5rem;
      color: #999;
    }

    .no-rules p {
      margin: 0;
      font-size: 1rem;
      color: #666;
    }

    /* Mobile adjustments */
    @media (max-width: 400px) {
      .times-grid {
        grid-template-columns: 1fr;
      }

      .time-value {
        font-size: 1.25rem;
      }
    }
  `]
})
export class HouseRulesComponent implements OnInit {
  private apiService = inject(GuestApiService);

  rules = signal<HouseRules | null>(null);
  ruleSections = signal<RuleSection[]>([]);
  loading = signal(true);

  private ruleConfig = [
    {
      key: 'smoking',
      icon: 'bi-wind',
      titleKey: 'houseRules.smoking',
      gradient: 'linear-gradient(135deg, #ef5350 0%, #c62828 100%)',
      lightBg: 'linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%)'
    },
    {
      key: 'pets',
      icon: 'bi-hearts',
      titleKey: 'houseRules.pets',
      gradient: 'linear-gradient(135deg, #7c4dff 0%, #651fff 100%)',
      lightBg: 'linear-gradient(135deg, #ede7f6 0%, #d1c4e9 100%)'
    },
    {
      key: 'children',
      icon: 'bi-emoji-smile',
      titleKey: 'houseRules.children',
      gradient: 'linear-gradient(135deg, #29b6f6 0%, #0288d1 100%)',
      lightBg: 'linear-gradient(135deg, #e1f5fe 0%, #b3e5fc 100%)'
    },
    {
      key: 'cancellation',
      icon: 'bi-calendar-check',
      titleKey: 'houseRules.cancellation',
      gradient: 'linear-gradient(135deg, #66bb6a 0%, #388e3c 100%)',
      lightBg: 'linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%)'
    }
  ];

  ngOnInit(): void {
    this.loadRules();
  }

  loadRules(): void {
    this.loading.set(true);
    this.apiService.getHouseRules().subscribe({
      next: (data) => {
        this.rules.set(data);
        this.buildRuleSections(data);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load house rules:', error);
        this.loading.set(false);
      }
    });
  }

  private buildRuleSections(data: HouseRules): void {
    const sections: RuleSection[] = [];

    for (const config of this.ruleConfig) {
      const content = (data as Record<string, string | undefined>)[config.key];
      if (content) {
        sections.push({
          key: config.key,
          icon: config.icon,
          titleKey: config.titleKey,
          content: content,
          gradient: config.gradient,
          lightBg: config.lightBg
        });
      }
    }

    this.ruleSections.set(sections);
  }
}
