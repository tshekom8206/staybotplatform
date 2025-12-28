import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

export interface MenuCardData {
  titleKey: string;
  descriptionKey: string;
  icon: string;
  route?: string;
  action?: () => void;
  backgroundImage?: string; // URL for card background image
  color?: string; // Kept for backward compatibility, but not used in new design
}

@Component({
  selector: 'app-menu-card',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    @if (data.route) {
      <a [routerLink]="data.route" class="menu-card" [class.has-bg]="data.backgroundImage">
        @if (data.backgroundImage) {
          <div class="menu-card-bg" [style.background-image]="'url(' + data.backgroundImage + ')'"></div>
          <div class="menu-card-overlay"></div>
        }
        <div class="menu-card-inner">
          <div class="menu-card-icon">
            <i class="bi" [class]="'bi-' + data.icon"></i>
          </div>
          <div class="menu-card-content">
            <h3 class="menu-card-title">{{ data.titleKey | translate }}</h3>
            <p class="menu-card-desc">{{ data.descriptionKey | translate }}</p>
          </div>
        </div>
      </a>
    } @else {
      <button class="menu-card" [class.has-bg]="data.backgroundImage" (click)="data.action?.()">
        @if (data.backgroundImage) {
          <div class="menu-card-bg" [style.background-image]="'url(' + data.backgroundImage + ')'"></div>
          <div class="menu-card-overlay"></div>
        }
        <div class="menu-card-inner">
          <div class="menu-card-icon">
            <i class="bi" [class]="'bi-' + data.icon"></i>
          </div>
          <div class="menu-card-content">
            <h3 class="menu-card-title">{{ data.titleKey | translate }}</h3>
            <p class="menu-card-desc">{{ data.descriptionKey | translate }}</p>
          </div>
        </div>
      </button>
    }
  `,
  styles: [`
    .menu-card {
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      padding: 1.5rem 1rem;
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(10px);
      -webkit-backdrop-filter: blur(10px);
      border-radius: 20px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
      text-decoration: none;
      color: inherit;
      border: 1px solid rgba(255, 255, 255, 0.2);
      width: 100%;
      min-height: 150px;
      cursor: pointer;
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
      overflow: hidden;
    }

    /* Background image layer */
    .menu-card-bg {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-size: cover;
      background-position: center;
      transition: transform 0.4s ease;
    }

    /* Overlay for readability */
    .menu-card-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: linear-gradient(
        135deg,
        rgba(255, 255, 255, 0.85) 0%,
        rgba(255, 255, 255, 0.75) 100%
      );
      backdrop-filter: blur(2px);
      -webkit-backdrop-filter: blur(2px);
      transition: all 0.3s ease;
    }

    /* Card with background */
    .menu-card.has-bg {
      background: transparent;
      border: none;
    }

    .menu-card.has-bg:hover .menu-card-bg {
      transform: scale(1.1);
    }

    .menu-card.has-bg:hover .menu-card-overlay {
      background: linear-gradient(
        135deg,
        rgba(255, 255, 255, 0.9) 0%,
        rgba(255, 255, 255, 0.85) 100%
      );
    }

    /* Content wrapper */
    .menu-card-inner {
      position: relative;
      z-index: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .menu-card:hover {
      transform: translateY(-6px);
      box-shadow: 0 16px 48px rgba(0, 0, 0, 0.15);
    }

    .menu-card:not(.has-bg):hover {
      background: rgba(255, 255, 255, 1);
    }

    .menu-card:active {
      transform: translateY(-2px);
    }

    /* Modern Monochrome Icon - White outline in black circle */
    .menu-card-icon {
      width: 60px;
      height: 60px;
      border-radius: 50%;
      background: #1a1a1a;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 1rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
      transition: all 0.3s ease;
    }

    .menu-card:hover .menu-card-icon {
      transform: scale(1.1);
      box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
    }

    .menu-card-icon i {
      font-size: 1.5rem;
      color: white;
      line-height: 1;
    }

    .menu-card-content {
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .menu-card-title {
      font-size: 0.95rem;
      font-weight: 600;
      margin-bottom: 0.25rem;
      color: #1a1a1a;
      letter-spacing: -0.01em;
    }

    .menu-card-desc {
      font-size: 0.75rem;
      color: #444;
      margin-bottom: 0;
      font-weight: 500;
    }

    /* Responsive adjustments */
    @media (min-width: 768px) {
      .menu-card {
        padding: 2rem 1.5rem;
        min-height: 180px;
      }

      .menu-card-icon {
        width: 70px;
        height: 70px;
      }

      .menu-card-icon i {
        font-size: 1.75rem;
      }

      .menu-card-title {
        font-size: 1.1rem;
      }

      .menu-card-desc {
        font-size: 0.85rem;
      }
    }
  `]
})
export class MenuCardComponent {
  @Input() data!: MenuCardData;
}
