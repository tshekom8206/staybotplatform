import { Component, inject, signal, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { WeatherUpsellApiService, WeatherUpsell, WeatherUpsellService } from '../../../core/services/weather-upsell.service';

@Component({
  selector: 'app-weather-upsell-banner',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    @if (upsell() && upsell()!.services.length > 0) {
      <div class="weather-upsell-banner">
        <div class="banner-header">
          <i class="bi bi-{{ upsell()!.bannerIcon || 'sun' }}"></i>
          <span class="banner-text">{{ upsell()!.bannerText }}</span>
        </div>

        <div class="services-scroll">
          <div class="services-track">
            @for (service of upsell()!.services; track service.id) {
              <div class="service-chip" (click)="selectService(service)">
                @if (service.imageUrl) {
                  <div class="chip-image" [style.background-image]="'url(' + service.imageUrl + ')'"></div>
                } @else {
                  <div class="chip-icon">
                    <i class="bi" [ngClass]="getIconClass(service.icon, service.category)"></i>
                  </div>
                }
                <div class="chip-content">
                  <span class="chip-name">{{ service.name }}</span>
                  @if (service.isChargeable && service.price) {
                    <span class="chip-price">{{ service.price }}</span>
                  } @else {
                    <span class="chip-action">View</span>
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
    .weather-upsell-banner {
      margin-top: 1rem;
      margin-left: auto;
      margin-right: auto;
      padding: 0.75rem 1rem;
      background: linear-gradient(135deg, rgba(255, 255, 255, 0.2) 0%, rgba(255, 255, 255, 0.1) 100%);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border: 1px solid rgba(255, 255, 255, 0.3);
      border-radius: 16px;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
      max-width: 360px;
    }

    .banner-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
      color: white;
    }

    .banner-header i {
      font-size: 1.25rem;
      color: #ffd700;
      filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.3));
    }

    .banner-text {
      font-size: 0.9rem;
      font-weight: 600;
      text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
    }

    .services-scroll {
      overflow-x: auto;
      overflow-y: hidden;
      margin: 0 -0.5rem;
      padding: 0 0.5rem;
      scrollbar-width: none;
      -ms-overflow-style: none;
    }

    .services-scroll::-webkit-scrollbar {
      display: none;
    }

    .services-track {
      display: flex;
      gap: 0.625rem;
      padding-bottom: 0.25rem;
    }

    .service-chip {
      flex: 0 0 auto;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      background: rgba(255, 255, 255, 0.15);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 50px;
      padding: 0.375rem;
      padding-right: 0.875rem;
      cursor: pointer;
      transition: all 0.2s ease;
      min-width: max-content;
    }

    .service-chip:hover {
      background: rgba(255, 255, 255, 0.25);
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .service-chip:active {
      transform: translateY(0);
    }

    .chip-image {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background-size: cover;
      background-position: center;
      flex-shrink: 0;
    }

    .chip-icon {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: rgba(0, 0, 0, 0.2);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .chip-icon i {
      font-size: 1rem;
      color: white;
    }

    .chip-content {
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }

    .chip-name {
      font-size: 0.8rem;
      font-weight: 600;
      color: white;
      text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
      line-height: 1.1;
    }

    .chip-price {
      font-size: 0.7rem;
      color: rgba(255, 255, 255, 0.8);
      font-weight: 500;
    }

    .chip-action {
      font-size: 0.65rem;
      color: #ffd700;
      font-weight: 500;
      text-transform: uppercase;
    }

    @media (max-width: 400px) {
      .weather-upsell-banner {
        padding: 0.75rem;
      }

      .banner-text {
        font-size: 0.85rem;
      }

      .chip-image, .chip-icon {
        width: 32px;
        height: 32px;
      }

      .chip-name {
        font-size: 0.75rem;
      }
    }
  `]
})
export class WeatherUpsellBannerComponent implements OnChanges, OnInit {
  private weatherUpsellService = inject(WeatherUpsellApiService);

  @Input() temperature: number | null = null;
  @Input() weatherCode: number | null = null;
  @Output() serviceSelected = new EventEmitter<{ service: WeatherUpsellService; weatherCondition: string | null }>();

  upsell = signal<WeatherUpsell | null>(null);
  loading = signal(false);

  ngOnInit(): void {
    // Check if values are already set (could happen if passed before init)
    if (this.temperature !== null && this.temperature !== undefined &&
        this.weatherCode !== null && this.weatherCode !== undefined) {
      this.loadUpsells();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Load upsells when weather data is available
    if ((changes['temperature'] || changes['weatherCode']) &&
        this.temperature !== null && this.temperature !== undefined &&
        this.weatherCode !== null && this.weatherCode !== undefined) {
      this.loadUpsells();
    }
  }

  private loadUpsells(): void {
    if (this.temperature === null || this.weatherCode === null) return;

    this.loading.set(true);
    this.weatherUpsellService.getWeatherUpsells(this.temperature, this.weatherCode).subscribe({
      next: (data) => {
        this.upsell.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  selectService(service: WeatherUpsellService): void {
    this.serviceSelected.emit({
      service,
      weatherCondition: this.upsell()?.weatherCondition || null
    });
  }

  getIconClass(icon: string | undefined, category: string | undefined): string {
    const iconMap: Record<string, string> = {
      'spa': 'bi-flower1',
      'wellness': 'bi-flower1',
      'pool': 'bi-water',
      'swimming': 'bi-water',
      'restaurant': 'bi-cup-hot',
      'dining': 'bi-cup-hot',
      'drinks': 'bi-cup-straw',
      'bar': 'bi-cup-straw',
      'room-service': 'bi-bell-fill',
      'gym': 'bi-heart-pulse',
      'fitness': 'bi-heart-pulse',
      'tour': 'bi-compass',
      'shuttle': 'bi-car-front',
      'sun': 'bi-sun',
      'cloud-rain': 'bi-cloud-rain',
      'snow': 'bi-snow'
    };

    const categoryMap: Record<string, string> = {
      'wellness': 'bi-flower1',
      'spa': 'bi-flower1',
      'pool': 'bi-water',
      'dining': 'bi-cup-hot',
      'restaurant': 'bi-cup-hot',
      'bar': 'bi-cup-straw',
      'transport': 'bi-car-front',
      'fitness': 'bi-heart-pulse'
    };

    if (icon) {
      const lowerIcon = icon.toLowerCase();
      if (iconMap[lowerIcon]) return iconMap[lowerIcon];
      if (lowerIcon.startsWith('bi-')) return lowerIcon;
      return `bi-${lowerIcon}`;
    }

    if (category) {
      const lowerCategory = category.toLowerCase();
      if (categoryMap[lowerCategory]) return categoryMap[lowerCategory];
    }

    return 'bi-star';
  }
}
