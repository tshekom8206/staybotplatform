import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, BookingInfo, ServiceCategory, ServiceItem } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';

@Component({
  selector: 'app-checkout',
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
          <h1 class="page-title">{{ 'checkout.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'checkout.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (error()) {
          <div class="error-container">
            <div class="error-card">
              <i class="bi bi-exclamation-triangle"></i>
              <h3>{{ 'checkout.errorTitle' | translate }}</h3>
              <p>{{ error() }}</p>
              <a routerLink="/" class="btn btn-dark">
                {{ 'common.backToHome' | translate }}
              </a>
            </div>
          </div>
        } @else {
          <!-- Booking Summary Card -->
          @if (bookingInfo()) {
            <div class="booking-summary-section">
              <div class="booking-summary-card">
                <div class="summary-header">
                  <i class="bi bi-info-circle"></i>
                  <span>{{ 'checkout.bookingSummary' | translate }}</span>
                </div>
                <div class="summary-content">
                  <div class="summary-row">
                    <span class="label">{{ 'checkout.guestName' | translate }}</span>
                    <span class="value">{{ bookingInfo()?.guestName }}</span>
                  </div>
                  <div class="summary-row">
                    <span class="label">{{ 'checkout.room' | translate }}</span>
                    <span class="value">{{ bookingInfo()?.roomNumber }}</span>
                  </div>
                  <div class="summary-row">
                    <span class="label">{{ 'checkout.checkoutDate' | translate }}</span>
                    <span class="value">{{ bookingInfo()?.checkoutDate | date:'mediumDate' }}</span>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- Services by Category -->
          @if (categories().length === 0) {
            <div class="no-services-container">
              <div class="no-services-card">
                <i class="bi bi-inbox"></i>
                <h3>{{ 'checkout.noServices' | translate }}</h3>
                <p>{{ 'checkout.noServicesDescription' | translate }}</p>
              </div>
            </div>
          } @else {
            @for (category of categories(); track category.category) {
              <div class="service-category-section">
                <h3 class="category-title">
                  <i class="bi" [class]="'bi-' + category.icon"></i>
                  {{ category.category }}
                </h3>

                <div class="services-grid">
                  @for (service of category.services; track service.id) {
                    <div class="service-card" (click)="onServiceRequest(service)">
                      @if (service.imageUrl) {
                        <div class="service-image-container">
                          <img [src]="service.imageUrl" [alt]="service.name" class="service-image">
                        </div>
                      }

                      <div class="service-content">
                        <h4 class="service-name">{{ service.name }}</h4>

                        @if (service.description) {
                          <p class="service-description">{{ service.description }}</p>
                        }

                        <div class="service-meta">
                          @if (service.priceAmount && service.priceAmount > 0) {
                            <div class="service-price">
                              <span class="price-amount">{{ service.currency }} {{ service.priceAmount | number:'1.2-2' }}</span>
                              @if (service.pricingUnit) {
                                <span class="price-unit">/ {{ service.pricingUnit }}</span>
                              }
                            </div>
                          }

                          @if (service.availableHours) {
                            <div class="service-hours">
                              <i class="bi bi-clock"></i>
                              <span>{{ service.availableHours }}</span>
                            </div>
                          }
                        </div>
                      </div>

                      <div class="service-action">
                        <button class="btn btn-sm request-service-btn">
                          {{ 'checkout.requestService' | translate }}
                          <i class="bi bi-arrow-right"></i>
                        </button>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0;
    }

    /* Page Header - White text on gradient */
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

    .back-link:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    .back-link i {
      font-size: 1rem;
    }

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

    /* Booking Summary */
    .booking-summary-section {
      margin-bottom: 1.5rem;
    }

    .booking-summary-card {
      background: white;
      border-radius: 12px;
      padding: 1rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }

    .summary-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;
      font-weight: 600;
      color: #1a1a1a;
    }

    .summary-header i {
      font-size: 1.2rem;
      color: #333;
    }

    .summary-content {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .summary-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.5rem 0;
      border-bottom: 1px solid #e9ecef;
    }

    .summary-row:last-child {
      border-bottom: none;
    }

    .summary-row .label {
      color: #666;
      font-size: 0.9rem;
    }

    .summary-row .value {
      font-weight: 600;
      color: #1a1a1a;
    }

    /* Service Categories */
    .service-category-section {
      margin-bottom: 2rem;
    }

    .category-title {
      font-size: 1.1rem;
      font-weight: 600;
      margin-bottom: 0.75rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .category-title i {
      font-size: 1rem;
    }

    /* Services Grid */
    .services-grid {
      display: grid;
      grid-template-columns: 1fr;
      gap: 1rem;
    }

    @media (min-width: 768px) {
      .services-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    .service-card {
      background: white;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      cursor: pointer;
      transition: all 0.3s ease;
      display: flex;
      flex-direction: column;
    }

    .service-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 16px rgba(0,0,0,0.12);
    }

    .service-image-container {
      width: 100%;
      height: 160px;
      overflow: hidden;
      background: #f5f7fa;
    }

    .service-image {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .service-content {
      padding: 1rem;
      flex: 1;
      display: flex;
      flex-direction: column;
    }

    .service-name {
      font-size: 1.1rem;
      font-weight: 600;
      margin: 0 0 0.5rem;
      color: #1a1a1a;
    }

    .service-description {
      font-size: 0.85rem;
      color: #666;
      margin: 0 0 1rem;
      line-height: 1.4;
      flex: 1;
    }

    .service-meta {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .service-price {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
    }

    .price-amount {
      font-size: 1.25rem;
      font-weight: 700;
      color: #333;
    }

    .price-unit {
      font-size: 0.85rem;
      color: #666;
    }

    .service-hours {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: #666;
    }

    .service-hours i {
      font-size: 0.9rem;
    }

    .service-action {
      padding: 0 1rem 1rem;
    }

    .request-service-btn {
      width: 100%;
      padding: 0.75rem;
      background-color: #333;
      border-color: #333;
      color: white;
      border-radius: 8px;
      font-weight: 500;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      transition: all 0.2s ease;
    }

    .request-service-btn:hover {
      background-color: #1a1a1a;
      border-color: #1a1a1a;
      color: white;
    }

    /* Error State */
    .error-container {
      padding: 2rem 0;
    }

    .error-card {
      background: white;
      border-radius: 12px;
      padding: 2rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      text-align: center;
    }

    .error-card i {
      font-size: 3rem;
      color: #e74c3c;
      margin-bottom: 1rem;
    }

    .error-card h3 {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .error-card p {
      color: #666;
      margin: 0 0 1.5rem;
    }

    /* No Services State */
    .no-services-container {
      padding: 2rem 0;
    }

    .no-services-card {
      background: white;
      border-radius: 12px;
      padding: 3rem 2rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      text-align: center;
    }

    .no-services-card i {
      font-size: 3rem;
      color: #adb5bd;
      margin-bottom: 1rem;
    }

    .no-services-card h3 {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .no-services-card p {
      color: #666;
      margin: 0;
    }

    /* Mobile Adjustments */
    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .service-name {
        font-size: 1rem;
      }

      .price-amount {
        font-size: 1.1rem;
      }
    }
  `]
})
export class CheckoutComponent implements OnInit {
  // State management with signals
  bookingInfo = signal<BookingInfo | null>(null);
  categories = signal<ServiceCategory[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  // Injected services
  private route = inject(ActivatedRoute);
  private apiService = inject(GuestApiService);
  private roomContext = inject(RoomContextService);

  ngOnInit(): void {
    // Extract booking ID from query params
    this.route.queryParams.subscribe(params => {
      if (params['booking']) {
        const bookingId = parseInt(params['booking'], 10);
        if (!isNaN(bookingId)) {
          this.loadBookingInfo(bookingId);
          this.loadCheckoutServices();
        } else {
          this.error.set('Invalid booking information');
          this.loading.set(false);
        }
      } else {
        this.error.set('No booking information provided');
        this.loading.set(false);
      }
    });
  }

  loadBookingInfo(bookingId: number): void {
    this.apiService.getBookingInfo(bookingId).subscribe({
      next: (booking) => {
        if (booking) {
          this.bookingInfo.set(booking);
          if (booking.roomNumber) {
            this.roomContext.setRoomNumber(booking.roomNumber);
          }
        } else {
          this.error.set('Booking not found');
        }
      },
      error: (err) => {
        console.error('Error loading booking info:', err);
        this.error.set('Unable to load booking information');
      }
    });
  }

  loadCheckoutServices(): void {
    this.loading.set(true);

    // Fetch all services and filter by checkout-relevant categories
    this.apiService.getServices().subscribe({
      next: (response) => {
        // Filter categories relevant to checkout
        const checkoutCategories = this.filterCheckoutCategories(response.categories);
        this.categories.set(checkoutCategories);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading services:', err);
        this.error.set('Unable to load services');
        this.loading.set(false);
      }
    });
  }

  private filterCheckoutCategories(categories: ServiceCategory[]): ServiceCategory[] {
    // Define checkout-relevant category keywords
    const checkoutKeywords = [
      'transport', 'shuttle', 'taxi', 'transfer',
      'checkout', 'late checkout', 'early checkout',
      'luggage', 'storage', 'baggage',
      'departure', 'leaving',
      'concierge', // Often includes checkout services
      'travel', 'airport'
    ];

    return categories
      .map(category => ({
        ...category,
        services: category.services.filter(service => {
          const categoryLower = category.category.toLowerCase();
          const serviceLower = service.name.toLowerCase();

          // Check if category or service name contains checkout keywords
          return checkoutKeywords.some(keyword =>
            categoryLower.includes(keyword) || serviceLower.includes(keyword)
          );
        })
      }))
      .filter(category => category.services.length > 0); // Only include categories with services
  }

  onServiceRequest(service: ServiceItem): void {
    // For now, just log - we'll integrate with service request modal in next phase
    console.log('Service requested:', service);
    // TODO: Open service request modal with booking and room context
    alert(`Request submitted for: ${service.name}\n\nThis will be integrated with the service request modal.`);
  }
}
