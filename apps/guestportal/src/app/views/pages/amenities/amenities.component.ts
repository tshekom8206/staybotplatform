import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, ServiceCategory, ServiceItem } from '../../../core/services/guest-api.service';

@Component({
  selector: 'app-amenities',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'amenities.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'amenities.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else if (categories().length === 0) {
          <div class="empty-state">
            <i class="bi bi-stars"></i>
            <p>{{ 'amenities.noServices' | translate }}</p>
          </div>
        } @else {
          @for (category of categories(); track category.category) {
            <div class="service-category">
              <div class="category-section">
                <div class="category-header">
                  <div class="category-icon-wrapper">
                    <i class="bi" [class]="category.icon"></i>
                  </div>
                  <h2>{{ category.category }}</h2>
                </div>
                <div class="services-list">
                @for (service of category.services; track service.id) {
                  <div class="service-card">
                    <div class="service-info">
                      <h4>{{ service.name }}</h4>
                      @if (service.description) {
                        <p class="description">{{ service.description }}</p>
                      }
                      <div class="service-meta">
                        @if (service.availableHours) {
                          <span class="meta-item">
                            <i class="bi bi-clock"></i> {{ service.availableHours }}
                          </span>
                        }
                        @if (service.requiresBooking) {
                          <span class="meta-item booking-required">
                            <i class="bi bi-calendar-check"></i> {{ 'amenities.bookingRequired' | translate }}
                          </span>
                        }
                      </div>
                    </div>
                    <div class="service-price">
                      @if (service.isChargeable) {
                        <span class="price">{{ service.price }}</span>
                        @if (service.pricingUnit) {
                          <span class="unit">{{ service.pricingUnit }}</span>
                        }
                      } @else {
                        <span class="complimentary">{{ 'amenities.complimentary' | translate }}</span>
                      }
                    </div>
                  </div>
                }
                </div>
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

    /* Page Header - Clean, floating on background */
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
      margin: 0;
      color: white;
      letter-spacing: -0.02em;
      text-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
    }
    .page-subtitle {
      font-size: 0.95rem;
      color: rgba(255, 255, 255, 0.9);
      margin: 0.25rem 0 0;
      text-shadow: 0 1px 6px rgba(0, 0, 0, 0.3);
    }

    .loading-spinner {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }
    .empty-state {
      text-align: center;
      padding: 3rem;
      background: #f8f9fa;
      border-radius: 16px;
      color: #666;
    }
    .empty-state i { font-size: 3rem; margin-bottom: 1rem; opacity: 0.5; }

    /* Category Section - Glassmorphism Container */
    .service-category {
      margin-bottom: 1.5rem;
    }

    .category-section {
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(20px);
      -webkit-backdrop-filter: blur(20px);
      border-radius: 20px;
      padding: 1.25rem;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.3);
    }

    .category-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid rgba(0, 0, 0, 0.08);
    }

    .category-icon-wrapper {
      width: 44px;
      height: 44px;
      background: #1a1a1a;
      color: white;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.1rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .category-header h2 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
      text-transform: capitalize;
      color: #1a1a1a;
      letter-spacing: -0.01em;
    }

    .services-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .service-card {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      padding: 1rem;
      background: rgba(255, 255, 255, 0.6);
      border-radius: 12px;
      border: 1px solid rgba(0, 0, 0, 0.04);
      transition: background 0.2s ease;
    }

    .service-card:hover {
      background: rgba(255, 255, 255, 0.9);
    }

    .service-info {
      flex: 1;
    }
    .service-info h4 {
      margin: 0 0 0.25rem;
      font-size: 0.95rem;
      font-weight: 600;
      color: #1a1a1a;
    }
    .service-info .description {
      margin: 0 0 0.5rem;
      font-size: 0.85rem;
      color: #555;
    }
    .service-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    .meta-item {
      font-size: 0.75rem;
      color: #666;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }
    .booking-required {
      background: rgba(243, 156, 18, 0.1);
      color: #b37400;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 500;
    }

    .service-price {
      text-align: right;
      flex-shrink: 0;
    }
    .service-price .price {
      display: block;
      font-weight: 700;
      color: #1a1a1a;
      font-size: 0.95rem;
    }
    .service-price .unit {
      font-size: 0.7rem;
      color: #888;
    }
    .service-price .complimentary {
      color: #1a1a1a;
      font-weight: 600;
      font-size: 0.85rem;
      background: rgba(39, 174, 96, 0.1);
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
    }
  `]
})
export class AmenitiesComponent implements OnInit {
  private apiService = inject(GuestApiService);

  categories = signal<ServiceCategory[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.loadServices();
  }

  loadServices(): void {
    this.loading.set(true);
    this.apiService.getServices().subscribe({
      next: (response) => {
        this.categories.set(response.categories);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load services:', error);
        this.loading.set(false);
      }
    });
  }
}
