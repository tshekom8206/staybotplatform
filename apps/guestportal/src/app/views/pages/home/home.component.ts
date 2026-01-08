import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { MenuCardComponent, MenuCardData } from '../../../shared/components/menu-card/menu-card.component';
import { EnhanceStayCarouselComponent } from '../../../shared/components/enhance-stay-carousel/enhance-stay-carousel.component';
import { ServiceRequestModalComponent } from '../../../shared/components/service-request-modal/service-request-modal.component';
import { WifiBottomSheetComponent } from '../../../shared/components/wifi-bottom-sheet/wifi-bottom-sheet.component';
import { RoomEditSheetComponent } from '../../../shared/components/room-edit-sheet/room-edit-sheet.component';
import { WeatherWidgetComponent } from '../../../shared/components/weather-widget/weather-widget.component';
import { WeatherUpsellBannerComponent } from '../../../shared/components/weather-upsell-banner/weather-upsell-banner.component';
import { TenantService } from '../../../core/services/tenant.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { FeaturedService } from '../../../core/services/guest-api.service';
import { WeatherData } from '../../../core/services/weather.service';
import { WeatherUpsellService } from '../../../core/services/weather-upsell.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule, MenuCardComponent, EnhanceStayCarouselComponent, ServiceRequestModalComponent, WifiBottomSheetComponent, RoomEditSheetComponent, WeatherWidgetComponent, WeatherUpsellBannerComponent],
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
            <button class="room-badge" (click)="openRoomEdit()">
              <i class="bi bi-door-open"></i>
              <span>Room {{ roomNumber }}</span>
              <i class="bi bi-pencil edit-icon"></i>
            </button>
          }
          <!-- Weather Widget -->
          <app-weather-widget (weatherLoaded)="onWeatherLoaded($event)" />
          <!-- WiFi Bottom Sheet (floating button in hero) -->
          <app-wifi-bottom-sheet />
        </div>

        <!-- Weather-based Upsell Banner -->
        <app-weather-upsell-banner
          [temperature]="weatherTemperature()"
          [weatherCode]="weatherCode()"
          (serviceSelected)="onWeatherServiceSelected($event)"
        />

        <!-- Menu Grid -->
        <div class="menu-section">
          <div class="menu-grid">
            @for (card of menuCards; track card.titleKey) {
              <app-menu-card [data]="card" />
            }
          </div>
        </div>

        <!-- Enhance Your Stay Carousel -->
        <app-enhance-stay-carousel
          (serviceSelected)="onServiceSelected($event)"
        />
      </div>
    </div>

    <!-- Service Request Modal -->
    <app-service-request-modal
      [isOpen]="modalOpen()"
      [service]="selectedService()"
      [source]="selectedSource()"
      (closed)="closeModal()"
    />

    <!-- Room Edit Sheet -->
    <app-room-edit-sheet
      [isOpen]="roomEditOpen()"
      [currentRoom]="roomNumber || ''"
      (closed)="closeRoomEdit()"
      (roomChanged)="onRoomChanged($event)"
    />
  `,
  styles: [`
    .home-page {
      padding: 0;
      min-height: calc(100vh - 120px);
    }

    /* Hero Section - Clean, floating on background */
    .hero-section {
      position: relative;
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
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .room-badge:hover {
      background: rgba(255, 255, 255, 0.3);
      transform: scale(1.02);
    }

    .room-badge:active {
      transform: scale(0.98);
    }

    .room-badge i {
      font-size: 1rem;
    }

    .room-badge .edit-icon {
      font-size: 0.75rem;
      opacity: 0.7;
      margin-left: 0.25rem;
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
  private analyticsService = inject(AnalyticsService);

  tenant = this.tenantService.getCurrentTenant();
  roomNumber = this.roomContextService.getRoomNumber();

  // Modal state
  modalOpen = signal(false);
  selectedService = signal<FeaturedService | null>(null);
  selectedSource = signal<string | null>(null); // For upsell tracking

  // Room edit state
  roomEditOpen = signal(false);

  // Weather state for upsell banner
  weatherTemperature = signal<number | null>(null);
  weatherCode = signal<number | null>(null);

  menuCards: MenuCardData[] = [
    {
      titleKey: 'home.menu.foodDrinks',
      descriptionKey: 'home.menu.foodDrinksDesc',
      icon: 'cup-hot',
      route: '/food-drinks'
    },
    {
      titleKey: 'home.menu.maintenance',
      descriptionKey: 'home.menu.maintenanceDesc',
      icon: 'wrench',
      route: '/maintenance'
    },
    {
      titleKey: 'home.menu.housekeeping',
      descriptionKey: 'home.menu.housekeepingDesc',
      icon: 'door-open',
      route: '/housekeeping'
    },
    {
      titleKey: 'home.menu.amenities',
      descriptionKey: 'home.menu.amenitiesDesc',
      icon: 'gem',
      route: '/amenities'
    },
    {
      titleKey: 'home.menu.lostFound',
      descriptionKey: 'home.menu.lostFoundDesc',
      icon: 'search',
      route: '/lost-found'
    },
    {
      titleKey: 'home.menu.rateUs',
      descriptionKey: 'home.menu.rateUsDesc',
      icon: 'star',
      route: '/rate-us'
    },
    {
      titleKey: 'home.menu.houseRules',
      descriptionKey: 'home.menu.houseRulesDesc',
      icon: 'clipboard-check',
      route: '/house-rules'
    },
    {
      titleKey: 'home.menu.contact',
      descriptionKey: 'home.menu.contactDesc',
      icon: 'chat-dots',
      action: () => this.openWhatsApp()
    }
  ];

  openWhatsApp(): void {
    this.analyticsService.trackWhatsAppOpened();
    this.tenantService.openWhatsApp(`Hello, I'm in Room ${this.roomNumber || ''}`);
  }

  onServiceSelected(service: FeaturedService): void {
    this.analyticsService.trackFeaturedServiceClick(service.name, service.price);
    this.selectedService.set(service);
    this.selectedSource.set('featured_carousel');
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.selectedService.set(null);
    this.selectedSource.set(null);
  }

  openRoomEdit(): void {
    this.roomEditOpen.set(true);
    document.body.style.overflow = 'hidden';
  }

  closeRoomEdit(): void {
    this.roomEditOpen.set(false);
    document.body.style.overflow = '';
  }

  onRoomChanged(newRoom: string): void {
    this.roomNumber = newRoom;
  }

  onWeatherLoaded(weather: WeatherData): void {
    this.weatherTemperature.set(weather.current.temp);
    this.weatherCode.set(weather.current.weatherCode);
  }

  onWeatherServiceSelected(event: { service: WeatherUpsellService; weatherCondition: string | null }): void {
    const { service, weatherCondition } = event;
    this.analyticsService.trackFeaturedServiceClick(service.name, service.price);
    // Convert to FeaturedService format for the modal
    const featuredService: FeaturedService = {
      id: service.id,
      name: service.name,
      description: service.description,
      category: service.category,
      icon: service.icon,
      imageUrl: service.imageUrl,
      isChargeable: service.isChargeable,
      price: service.price,
      priceAmount: service.priceAmount,
      currency: service.currency,
      pricingUnit: service.pricingUnit,
      availableHours: service.availableHours,
      requiresBooking: service.requiresBooking,
      advanceBookingHours: service.advanceBookingHours
    };
    this.selectedService.set(featuredService);
    // Set source based on weather condition (e.g., "weather_warm", "weather_hot")
    this.selectedSource.set(weatherCondition ? `weather_${weatherCondition}` : 'weather_upsell');
    this.modalOpen.set(true);
  }
}
