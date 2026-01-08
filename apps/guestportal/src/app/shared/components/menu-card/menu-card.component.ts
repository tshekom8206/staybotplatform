import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AnalyticsService } from '../../../core/services/analytics.service';

export interface MenuCardData {
  titleKey: string;
  descriptionKey: string;
  icon: string;
  route?: string;
  action?: () => void;
}

@Component({
  selector: 'app-menu-card',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    @if (data.route) {
      <a [routerLink]="data.route" class="menu-card" (click)="onCardClick()">
        <div class="menu-card-icon">
          <i class="bi" [class]="'bi-' + data.icon"></i>
        </div>
        <h3 class="menu-card-title">{{ data.titleKey | translate }}</h3>
        <p class="menu-card-desc">{{ data.descriptionKey | translate }}</p>
      </a>
    } @else {
      <button class="menu-card" (click)="onButtonClick()">
        <div class="menu-card-icon">
          <i class="bi" [class]="'bi-' + data.icon"></i>
        </div>
        <h3 class="menu-card-title">{{ data.titleKey | translate }}</h3>
        <p class="menu-card-desc">{{ data.descriptionKey | translate }}</p>
      </button>
    }
  `,
  styles: [`
    .menu-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      padding: 1rem 0.5rem;
      background: transparent;
      border: none;
      text-decoration: none;
      color: inherit;
      width: 100%;
      cursor: pointer;
      transition: transform 0.3s ease;
    }

    .menu-card:hover {
      transform: translateY(-4px);
    }

    .menu-card:active {
      transform: translateY(-2px);
    }

    /* Circular icon - black background, white icon */
    .menu-card-icon {
      width: 70px;
      height: 70px;
      border-radius: 50%;
      background: #1a1a1a;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 0.875rem;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
      transition: all 0.3s ease;
    }

    .menu-card:hover .menu-card-icon {
      transform: scale(1.08);
      box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
    }

    .menu-card-icon i {
      font-size: 1.6rem;
      color: white;
      line-height: 1;
    }

    .menu-card-title {
      font-size: 0.95rem;
      font-weight: 600;
      margin: 0 0 0.25rem;
      color: white;
      text-shadow: 0 2px 8px rgba(0, 0, 0, 0.4);
      letter-spacing: 0.01em;
    }

    .menu-card-desc {
      font-size: 0.8rem;
      color: rgba(255, 255, 255, 0.85);
      margin: 0;
      font-weight: 400;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    /* Responsive - larger icons on bigger screens */
    @media (min-width: 576px) {
      .menu-card-icon {
        width: 80px;
        height: 80px;
      }

      .menu-card-icon i {
        font-size: 1.8rem;
      }

      .menu-card-title {
        font-size: 1.05rem;
      }

      .menu-card-desc {
        font-size: 0.85rem;
      }
    }
  `]
})
export class MenuCardComponent {
  private analyticsService = inject(AnalyticsService);

  @Input() data!: MenuCardData;

  onCardClick(): void {
    // Extract menu item name from the translation key (e.g., 'home.menu.foodDrinks' -> 'foodDrinks')
    const menuItem = this.data.titleKey.split('.').pop() || this.data.titleKey;
    this.analyticsService.trackMenuClick(menuItem);
  }

  onButtonClick(): void {
    // Track the click first
    const menuItem = this.data.titleKey.split('.').pop() || this.data.titleKey;
    this.analyticsService.trackMenuClick(menuItem);
    // Then execute the action
    this.data.action?.();
  }
}
