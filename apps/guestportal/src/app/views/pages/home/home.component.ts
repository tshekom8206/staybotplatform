import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { MenuCardComponent, MenuCardData } from '../../../shared/components/menu-card/menu-card.component';
import { TenantService } from '../../../core/services/tenant.service';
import { RoomContextService } from '../../../core/services/room-context.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule, MenuCardComponent],
  template: `
    <div class="home-page">
      <div class="container">
        <!-- Hero Section - Clean, no card -->
        <div class="hero-section">
          @if (tenant?.logoUrl) {
            <img [src]="tenant!.logoUrl" alt="Logo" class="hero-logo" />
          }
          <h1 class="hero-title">{{ 'home.welcome' | translate }}</h1>
          <p class="hero-subtitle">{{ 'home.howCanWeHelp' | translate }}</p>
          @if (roomNumber) {
            <div class="room-badge">
              <i class="bi bi-door-open"></i>
              <span>Room {{ roomNumber }}</span>
            </div>
          }
        </div>

        <!-- Menu Grid -->
        <div class="menu-section">
          <div class="menu-grid">
            @for (card of menuCards; track card.titleKey) {
              <app-menu-card [data]="card" />
            }
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .home-page {
      padding: 0;
      min-height: calc(100vh - 120px);
    }

    /* Hero Section - Clean, floating on background */
    .hero-section {
      padding: 2.5rem 0 2rem;
      text-align: center;
    }

    .hero-logo {
      max-height: 70px;
      max-width: 220px;
      margin-bottom: 1.25rem;
      object-fit: contain;
      filter: drop-shadow(0 2px 8px rgba(0, 0, 0, 0.3));
    }

    .hero-title {
      font-size: 2.25rem;
      font-weight: 700;
      margin-bottom: 0.5rem;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }

    .hero-subtitle {
      font-size: 1.05rem;
      color: rgba(255, 255, 255, 0.9);
      margin-bottom: 1rem;
      text-shadow: 0 1px 6px rgba(0, 0, 0, 0.3);
    }

    .room-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: rgba(255, 255, 255, 0.2);
      backdrop-filter: blur(10px);
      -webkit-backdrop-filter: blur(10px);
      color: white;
      padding: 0.5rem 1rem;
      border-radius: 50px;
      font-size: 0.9rem;
      font-weight: 500;
      border: 1px solid rgba(255, 255, 255, 0.3);
    }

    .room-badge i {
      font-size: 1rem;
    }

    /* Menu Section */
    .menu-section {
      padding-bottom: 2rem;
    }

    .menu-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
    }

    @media (min-width: 576px) {
      .menu-grid {
        grid-template-columns: repeat(3, 1fr);
        gap: 1.25rem;
      }
    }

    @media (min-width: 768px) {
      .hero-section {
        padding: 3rem 0;
      }

      .hero-content {
        padding: 3rem 2rem;
        max-width: 600px;
        margin: 0 auto;
      }

      .hero-title {
        font-size: 2.5rem;
      }

      .hero-subtitle {
        font-size: 1.1rem;
      }

      .menu-grid {
        gap: 1.5rem;
      }
    }

    @media (min-width: 992px) {
      .menu-grid {
        grid-template-columns: repeat(3, 1fr);
        max-width: 900px;
        margin: 0 auto;
      }
    }
  `]
})
export class HomeComponent {
  private tenantService = inject(TenantService);
  private roomContextService = inject(RoomContextService);

  tenant = this.tenantService.getCurrentTenant();
  roomNumber = this.roomContextService.getRoomNumber();

  menuCards: MenuCardData[] = [
    {
      titleKey: 'home.menu.foodDrinks',
      descriptionKey: 'home.menu.foodDrinksDesc',
      icon: 'cup-hot',
      route: '/food-drinks',
      backgroundImage: 'https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=400&q=80'
    },
    {
      titleKey: 'home.menu.maintenance',
      descriptionKey: 'home.menu.maintenanceDesc',
      icon: 'wrench',
      route: '/maintenance',
      backgroundImage: 'https://images.unsplash.com/photo-1581578731548-c64695cc6952?w=400&q=80'
    },
    {
      titleKey: 'home.menu.amenities',
      descriptionKey: 'home.menu.amenitiesDesc',
      icon: 'gem',
      route: '/amenities',
      backgroundImage: 'https://images.unsplash.com/photo-1571896349842-33c89424de2d?w=400&q=80'
    },
    {
      titleKey: 'home.menu.lostFound',
      descriptionKey: 'home.menu.lostFoundDesc',
      icon: 'search',
      route: '/lost-found',
      backgroundImage: 'https://images.unsplash.com/photo-1586769852044-692d6e3703f0?w=400&q=80'
    },
    {
      titleKey: 'home.menu.rateUs',
      descriptionKey: 'home.menu.rateUsDesc',
      icon: 'star',
      route: '/rate-us',
      backgroundImage: 'https://images.unsplash.com/photo-1517048676732-d65bc937f952?w=400&q=80'
    },
    {
      titleKey: 'home.menu.contact',
      descriptionKey: 'home.menu.contactDesc',
      icon: 'chat-dots',
      action: () => this.openWhatsApp(),
      backgroundImage: 'https://images.unsplash.com/photo-1423666639041-f56000c27a9a?w=400&q=80'
    }
  ];

  openWhatsApp(): void {
    this.tenantService.openWhatsApp(`Hello, I'm in Room ${this.roomNumber || ''}`);
  }
}
