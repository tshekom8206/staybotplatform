import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, ServiceCategory, ServiceItem, FeaturedService } from '../../../core/services/guest-api.service';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { ServiceRequestModalComponent } from '../../../shared/components/service-request-modal/service-request-modal.component';

@Component({
  selector: 'app-services',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule, ServiceRequestModalComponent],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'services.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'services.subtitle' | translate }}</p>
        </div>

        <!-- Search Bar -->
        <div class="search-container">
          <div class="search-input-wrapper">
            <i class="bi bi-search search-icon"></i>
            <input
              type="text"
              class="search-input"
              [(ngModel)]="searchQuery"
              (ngModelChange)="onSearchChange($event)"
              [placeholder]="'services.searchPlaceholder' | translate"
              autocomplete="off"
            />
            @if (searchQuery()) {
              <button class="clear-search" (click)="clearSearch()">
                <i class="bi bi-x-circle-fill"></i>
              </button>
            }
          </div>
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
        } @else if (filteredAndSearchedCategories().length === 0) {
          <div class="empty-state">
            <i class="bi bi-search"></i>
            <h3>{{ 'services.noResults' | translate }}</h3>
            <p>{{ 'services.noResultsMessage' | translate }}</p>
            <button class="custom-request-btn" (click)="openCustomRequest()">
              <i class="bi bi-plus-circle"></i>
              {{ 'services.customRequest' | translate }}
            </button>
          </div>
        } @else {
          <!-- Category Pills -->
          <div class="category-pills">
            <button
              class="pill"
              [class.active]="selectedCategory() === ''"
              (click)="selectCategory('')">
              All
            </button>
            @for (category of uniqueCategories(); track category) {
              <button
                class="pill"
                [class.active]="selectedCategory() === category"
                (click)="selectCategory(category)">
                <i class="bi" [ngClass]="getCategoryIcon(category)"></i>
                {{ category }}
              </button>
            }
          </div>

          <!-- Services Grid -->
          <div class="services-grid">
            @for (category of filteredAndSearchedCategories(); track category.category) {
              <div class="category-section">
                <div class="category-header">
                  <div class="category-icon">
                    <i class="bi" [ngClass]="category.icon || getCategoryIcon(category.category)"></i>
                  </div>
                  <h2>{{ category.category }}</h2>
                </div>

                <div class="services-list">
                  @for (service of category.services; track service.id) {
                    <div class="service-card" (click)="openServiceModal(service)">
                      @if (service.imageUrl) {
                        <div class="service-image" [style.background-image]="'url(' + service.imageUrl + ')'">
                          @if (service.isChargeable && service.price) {
                            <span class="price-tag">{{ service.price }}</span>
                          }
                        </div>
                      } @else {
                        <div class="service-icon-placeholder">
                          <i class="bi" [ngClass]="getServiceIcon(service.icon, service.category)"></i>
                          @if (service.isChargeable && service.price) {
                            <span class="price-tag">{{ service.price }}</span>
                          }
                        </div>
                      }

                      <div class="service-content">
                        <h4>{{ service.name }}</h4>
                        @if (service.description) {
                          <p class="service-desc">{{ service.description }}</p>
                        }
                        <div class="service-meta">
                          @if (service.availableHours) {
                            <span class="meta-item">
                              <i class="bi bi-clock"></i> {{ service.availableHours }}
                            </span>
                          }
                          @if (service.requiresBooking) {
                            <span class="meta-item booking">
                              <i class="bi bi-calendar-check"></i> {{ 'amenities.bookingRequired' | translate }}
                            </span>
                          }
                          @if (!service.isChargeable) {
                            <span class="meta-item complimentary">
                              {{ 'amenities.complimentary' | translate }}
                            </span>
                          }
                        </div>
                      </div>

                      <div class="service-action">
                        <button class="request-btn">
                          {{ 'services.requestService' | translate }}
                          <i class="bi bi-chevron-right"></i>
                        </button>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }
      </div>
    </div>

    <!-- Service Request Modal -->
    <app-service-request-modal
      [isOpen]="modalOpen()"
      [service]="selectedService()"
      (closed)="closeModal()"
    />
  `,
  styles: [`
    .page-container { padding: 1rem 0 2rem; }

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
    /* Search Bar */
    .search-container {
      margin-bottom: 1.25rem;
    }
    .search-input-wrapper {
      position: relative;
      display: flex;
      align-items: center;
    }
    .search-icon {
      position: absolute;
      left: 1rem;
      color: rgba(255, 255, 255, 0.7);
      font-size: 1.1rem;
      pointer-events: none;
      z-index: 1;
    }
    .search-input {
      width: 100%;
      padding: 0.85rem 3rem 0.85rem 3rem;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-radius: 50px;
      font-size: 0.95rem;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      color: white;
      transition: all 0.2s ease;
    }
    .search-input::placeholder {
      color: rgba(255, 255, 255, 0.6);
    }
    .search-input:focus {
      outline: none;
      background: rgba(255, 255, 255, 0.25);
      border-color: rgba(255, 255, 255, 0.5);
    }
    .clear-search {
      position: absolute;
      right: 1rem;
      background: none;
      border: none;
      color: rgba(255, 255, 255, 0.7);
      font-size: 1.2rem;
      cursor: pointer;
      padding: 0.25rem;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: color 0.2s ease;
    }
    .clear-search:hover {
      color: white;
    }

    .empty-state {
      text-align: center;
      padding: 3rem 1.5rem;
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(20px);
      border-radius: 16px;
      color: #666;
    }
    .empty-state i {
      font-size: 3rem;
      margin-bottom: 1rem;
      opacity: 0.5;
      color: #1a1a1a;
    }
    .empty-state h3 {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }
    .empty-state p {
      margin: 0 0 1.5rem;
      color: #666;
    }
    .custom-request-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: #1a1a1a;
      color: white;
      border: none;
      padding: 0.75rem 1.5rem;
      border-radius: 50px;
      font-size: 0.95rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .custom-request-btn:hover {
      background: #333;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    /* Category Pills */
    .category-pills {
      display: flex;
      gap: 0.5rem;
      overflow-x: auto;
      padding-bottom: 1rem;
      scrollbar-width: none;
      -ms-overflow-style: none;
    }
    .category-pills::-webkit-scrollbar { display: none; }

    .pill {
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.2);
      color: white;
      padding: 0.5rem 1rem;
      border-radius: 50px;
      font-size: 0.85rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s ease;
      white-space: nowrap;
    }
    .pill:hover {
      background: rgba(255, 255, 255, 0.25);
    }
    .pill.active {
      background: white;
      color: #1a1a1a;
      border-color: white;
    }
    .pill i { font-size: 0.9rem; }

    /* Services Grid */
    .services-grid {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
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

    .category-icon {
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
      font-weight: 600;
      color: #1a1a1a;
    }

    .services-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .service-card {
      display: flex;
      flex-direction: column;
      background: rgba(255, 255, 255, 0.6);
      border-radius: 16px;
      overflow: hidden;
      border: 1px solid rgba(0, 0, 0, 0.04);
      transition: all 0.2s ease;
      cursor: pointer;
    }
    .service-card:hover {
      background: rgba(255, 255, 255, 0.95);
      transform: translateY(-2px);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
    }

    .service-image {
      height: 140px;
      background-size: cover;
      background-position: center;
      position: relative;
    }

    .service-icon-placeholder {
      height: 100px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
      position: relative;
    }
    .service-icon-placeholder i {
      font-size: 2.5rem;
      color: #1a1a1a;
      opacity: 0.3;
    }

    .price-tag {
      position: absolute;
      bottom: 0.75rem;
      right: 0.75rem;
      background: rgba(0, 0, 0, 0.8);
      color: white;
      font-size: 0.85rem;
      font-weight: 600;
      padding: 0.35rem 0.75rem;
      border-radius: 6px;
    }

    .service-content {
      padding: 1rem;
      flex: 1;
    }
    .service-content h4 {
      margin: 0 0 0.35rem;
      font-size: 1rem;
      font-weight: 600;
      color: #1a1a1a;
    }
    .service-desc {
      margin: 0 0 0.75rem;
      font-size: 0.85rem;
      color: #555;
      line-height: 1.4;
    }
    .service-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .meta-item {
      font-size: 0.75rem;
      color: #666;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }
    .meta-item.booking {
      background: rgba(243, 156, 18, 0.1);
      color: #b37400;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 500;
    }
    .meta-item.complimentary {
      background: rgba(39, 174, 96, 0.1);
      color: #1e8449;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 500;
    }

    .service-action {
      padding: 0 1rem 1rem;
    }
    .request-btn {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      background: #1a1a1a;
      color: white;
      border: none;
      padding: 0.75rem 1rem;
      border-radius: 10px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .request-btn:hover {
      background: #333;
      transform: translateY(-1px);
    }
    .request-btn i {
      font-size: 0.85rem;
      transition: transform 0.2s ease;
    }
    .service-card:hover .request-btn i {
      transform: translateX(4px);
    }

    @media (min-width: 576px) {
      .services-list {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: 1rem;
      }

      .service-card {
        height: 100%;
      }
    }

    @media (min-width: 992px) {
      .services-list {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  `]
})
export class ServicesComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private analyticsService = inject(AnalyticsService);

  categories = signal<ServiceCategory[]>([]);
  loading = signal(true);
  selectedCategory = signal('');
  searchQuery = signal('');

  // Modal state
  modalOpen = signal(false);
  selectedService = signal<FeaturedService | null>(null);

  // Computed signal for filtered and searched categories
  filteredAndSearchedCategories = computed(() => {
    let result = this.categories();

    // Filter by selected category pill
    const selected = this.selectedCategory();
    if (selected) {
      result = result.filter(c => c.category === selected);
    }

    // Filter by search query
    const query = this.searchQuery().toLowerCase().trim();
    if (query) {
      result = result.map(category => {
        const filteredServices = category.services.filter(service => {
          const matchesName = service.name.toLowerCase().includes(query);
          const matchesDescription = service.description?.toLowerCase().includes(query) || false;
          const matchesCategory = category.category.toLowerCase().includes(query);
          return matchesName || matchesDescription || matchesCategory;
        });

        return {
          ...category,
          services: filteredServices
        };
      }).filter(category => category.services.length > 0);
    }

    return result;
  });

  ngOnInit(): void {
    this.loadServices();
  }

  loadServices(): void {
    this.loading.set(true);
    this.apiService.getServices().subscribe({
      next: (response) => {
        // Filter to only show chargeable/premium services
        const premiumCategories = response.categories.map(cat => ({
          ...cat,
          services: cat.services.filter(s => s.isChargeable || s.requiresBooking)
        })).filter(cat => cat.services.length > 0);

        this.categories.set(premiumCategories);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load services:', error);
        this.loading.set(false);
      }
    });
  }

  uniqueCategories(): string[] {
    return [...new Set(this.categories().map(c => c.category))];
  }

  selectCategory(category: string): void {
    this.selectedCategory.set(category);
  }

  onSearchChange(query: string): void {
    this.searchQuery.set(query);

    // Track search event for analytics
    if (query.trim()) {
      this.analyticsService.trackEvent('search', {
        search_term: query,
        page_location: '/services'
      });
    }
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  openCustomRequest(): void {
    // Open the modal with no service selected (custom request)
    this.selectedService.set(null);
    this.modalOpen.set(true);
  }

  openServiceModal(service: ServiceItem): void {
    // Track form_start event for analytics
    this.analyticsService.trackEvent('form_start', {
      form_name: 'service_request',
      service_name: service.name,
      service_category: service.category,
      page_location: '/services'
    });

    // Convert ServiceItem to FeaturedService for the modal
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
      advanceBookingHours: service.advanceBookingHours,
      timeSlots: service.timeSlots
    };
    this.selectedService.set(featuredService);
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.selectedService.set(null);
  }

  getCategoryIcon(category: string): string {
    const iconMap: Record<string, string> = {
      'dining': 'bi-cup-hot',
      'in-room dining': 'bi-cup-hot',
      'restaurant': 'bi-cup-hot',
      'food': 'bi-egg-fried',
      'spa': 'bi-flower1',
      'spa & wellness': 'bi-flower1',
      'wellness': 'bi-flower1',
      'special occasions': 'bi-gift',
      'occasions': 'bi-gift',
      'celebration': 'bi-balloon',
      'transport': 'bi-car-front',
      'transportation': 'bi-car-front',
      'shuttle': 'bi-bus-front',
      'experiences': 'bi-compass',
      'local experiences': 'bi-compass',
      'tours': 'bi-signpost-2',
      'checkout': 'bi-box-arrow-right',
      'checkout options': 'bi-box-arrow-right',
      'accommodation': 'bi-door-open',
      'room': 'bi-door-open',
      'activities': 'bi-heart-pulse',
      'fitness': 'bi-heart-pulse',
      'pool': 'bi-water',
      'business': 'bi-briefcase',
      'concierge': 'bi-person-badge'
    };

    const lowerCategory = category.toLowerCase();
    return iconMap[lowerCategory] || 'bi-stars';
  }

  getServiceIcon(icon: string | undefined, category: string | undefined): string {
    const iconMap: Record<string, string> = {
      'spa': 'bi-flower1',
      'massage': 'bi-flower2',
      'restaurant': 'bi-cup-hot',
      'dining': 'bi-cup-hot',
      'car': 'bi-car-front',
      'shuttle': 'bi-bus-front',
      'tour': 'bi-compass',
      'gift': 'bi-gift',
      'bed': 'bi-lamp',
      'room': 'bi-door-open'
    };

    if (icon) {
      const lowerIcon = icon.toLowerCase();
      if (iconMap[lowerIcon]) return iconMap[lowerIcon];
      if (lowerIcon.startsWith('bi-')) return lowerIcon;
      return `bi-${lowerIcon}`;
    }

    if (category) {
      return this.getCategoryIcon(category);
    }

    return 'bi-stars';
  }
}
