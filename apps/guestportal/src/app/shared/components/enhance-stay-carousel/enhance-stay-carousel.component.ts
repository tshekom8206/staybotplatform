import { Component, inject, signal, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, FeaturedService } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-enhance-stay-carousel',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    @if (services().length > 0) {
      <div class="enhance-section">
        <div class="section-header">
          <div class="header-left">
            <i class="bi bi-stars"></i>
            <span>{{ 'services.enhanceYourStay' | translate }}</span>
          </div>
          <a routerLink="/services" class="see-all-link">
            {{ 'common.seeAll' | translate }} <i class="bi bi-chevron-right"></i>
          </a>
        </div>

        <div class="carousel-container">
          <div class="carousel-track">
            @for (service of services(); track service.id) {
              <div class="service-card" (click)="selectService(service)">
                @if (service.imageUrl) {
                  <div class="card-image" [style.background-image]="'url(' + service.imageUrl + ')'">
                    <div class="card-overlay">
                      @if (service.isChargeable && service.price) {
                        <span class="price-badge">{{ service.price }}</span>
                      }
                    </div>
                  </div>
                } @else {
                  <div class="card-icon-placeholder">
                    <i class="bi" [ngClass]="getIconClass(service.icon, service.category)"></i>
                    @if (service.isChargeable && service.price) {
                      <span class="price-badge">{{ service.price }}</span>
                    }
                  </div>
                }
                <div class="card-content">
                  <h4 class="card-title">{{ service.name }}</h4>
                  @if (service.description) {
                    <p class="card-desc">{{ service.description | slice:0:60 }}{{ service.description.length > 60 ? '...' : '' }}</p>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .enhance-section {
      padding: 1.5rem 0;
    }

    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1rem;
      padding: 0 1rem;
    }

    .header-left {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      font-size: 1rem;
      font-weight: 600;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .header-left i {
      font-size: 1.1rem;
      color: #ffd700;
    }

    .see-all-link {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      color: rgba(255, 255, 255, 0.9);
      text-decoration: none;
      font-size: 0.85rem;
      font-weight: 500;
      padding: 0.35rem 0.75rem;
      border-radius: 50px;
      background: rgba(255, 255, 255, 0.1);
      transition: all 0.2s ease;
    }

    .see-all-link:hover {
      background: rgba(255, 255, 255, 0.2);
      color: white;
    }

    .see-all-link i {
      font-size: 0.75rem;
      transition: transform 0.2s ease;
    }

    .see-all-link:hover i {
      transform: translateX(2px);
    }

    .carousel-container {
      overflow-x: auto;
      overflow-y: hidden;
      scrollbar-width: none;
      -ms-overflow-style: none;
      padding: 0 1rem;
    }

    .carousel-container::-webkit-scrollbar {
      display: none;
    }

    .carousel-track {
      display: flex;
      gap: 0.875rem;
      padding-bottom: 0.5rem;
    }

    .service-card {
      flex: 0 0 auto;
      width: 160px;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      -webkit-backdrop-filter: blur(10px);
      border-radius: 16px;
      overflow: hidden;
      cursor: pointer;
      transition: all 0.3s ease;
      border: 1px solid rgba(255, 255, 255, 0.2);
    }

    .service-card:hover {
      transform: translateY(-4px);
      background: rgba(255, 255, 255, 0.2);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.25);
    }

    .service-card:active {
      transform: translateY(-2px);
    }

    .card-image {
      height: 100px;
      background-size: cover;
      background-position: center;
      position: relative;
    }

    .card-overlay {
      position: absolute;
      inset: 0;
      background: linear-gradient(to bottom, transparent 40%, rgba(0, 0, 0, 0.6));
      display: flex;
      align-items: flex-end;
      justify-content: flex-end;
      padding: 0.5rem;
    }

    .card-icon-placeholder {
      height: 100px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(0, 0, 0, 0.3);
      position: relative;
    }

    .card-icon-placeholder i {
      font-size: 2.5rem;
      color: white;
      opacity: 0.8;
    }

    .card-icon-placeholder .price-badge {
      position: absolute;
      bottom: 0.5rem;
      right: 0.5rem;
    }

    .price-badge {
      background: rgba(0, 0, 0, 0.7);
      color: white;
      font-size: 0.7rem;
      font-weight: 600;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
    }

    .card-content {
      padding: 0.75rem;
    }

    .card-title {
      font-size: 0.85rem;
      font-weight: 600;
      color: white;
      margin: 0 0 0.25rem;
      line-height: 1.2;
      text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
    }

    .card-desc {
      font-size: 0.7rem;
      color: rgba(255, 255, 255, 0.8);
      margin: 0;
      line-height: 1.3;
    }

    @media (min-width: 576px) {
      .service-card {
        width: 180px;
      }

      .card-image, .card-icon-placeholder {
        height: 110px;
      }

      .card-title {
        font-size: 0.9rem;
      }

      .card-desc {
        font-size: 0.75rem;
      }
    }
  `]
})
export class EnhanceStayCarouselComponent implements OnInit {
  private apiService = inject(GuestApiService);

  services = signal<FeaturedService[]>([]);
  loading = signal(false);

  @Output() serviceSelected = new EventEmitter<FeaturedService>();

  ngOnInit(): void {
    this.loadFeaturedServices();
  }

  private loadFeaturedServices(): void {
    this.loading.set(true);
    this.apiService.getFeaturedServices().subscribe({
      next: (response) => {
        this.services.set(response.services);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  selectService(service: FeaturedService): void {
    this.serviceSelected.emit(service);
  }

  getIconClass(icon: string | undefined, category: string | undefined): string {
    // Map common icon names to Bootstrap Icons
    const iconMap: Record<string, string> = {
      // Spa & Wellness
      'spa': 'bi-flower1',
      'wellness': 'bi-flower1',
      'massage': 'bi-flower2',
      'flower1': 'bi-flower1',
      'flower2': 'bi-flower2',
      // Dining
      'restaurant': 'bi-cup-hot',
      'dining': 'bi-cup-hot',
      'food': 'bi-egg-fried',
      'breakfast': 'bi-cup-hot',
      'coffee': 'bi-cup-hot',
      // Transport
      'car': 'bi-car-front',
      'shuttle': 'bi-bus-front',
      'transport': 'bi-car-front',
      'taxi': 'bi-taxi-front',
      // Accommodation
      'bed': 'bi-lamp',
      'room': 'bi-door-open',
      'hotel': 'bi-building',
      // Activities
      'tour': 'bi-compass',
      'hiking': 'bi-signpost-2',
      'pool': 'bi-water',
      'gym': 'bi-heart-pulse',
      'fitness': 'bi-heart-pulse',
      // Other
      'gift': 'bi-gift',
      'star': 'bi-star',
      'heart': 'bi-heart',
      'clock': 'bi-clock',
      'calendar': 'bi-calendar-event'
    };

    // Category-based fallbacks
    const categoryMap: Record<string, string> = {
      'wellness': 'bi-flower1',
      'spa': 'bi-flower1',
      'dining': 'bi-cup-hot',
      'restaurant': 'bi-cup-hot',
      'transport': 'bi-car-front',
      'transportation': 'bi-car-front',
      'accommodation': 'bi-door-open',
      'local tours': 'bi-compass',
      'tours': 'bi-compass',
      'activities': 'bi-heart-pulse',
      'business': 'bi-briefcase'
    };

    // Try icon name first
    if (icon) {
      const lowerIcon = icon.toLowerCase();
      if (iconMap[lowerIcon]) {
        return iconMap[lowerIcon];
      }
      // Try as Bootstrap Icon directly
      if (lowerIcon.startsWith('bi-')) {
        return lowerIcon;
      }
      // Try adding bi- prefix
      return `bi-${lowerIcon}`;
    }

    // Fall back to category
    if (category) {
      const lowerCategory = category.toLowerCase();
      if (categoryMap[lowerCategory]) {
        return categoryMap[lowerCategory];
      }
    }

    // Default fallback
    return 'bi-gift';
  }
}
