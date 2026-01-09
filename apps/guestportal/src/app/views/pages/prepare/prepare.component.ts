import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, PrepareItem, ServiceRequest, ItemRequest, BookingInfo } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

@Component({
  selector: 'app-prepare',
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
          <h1 class="page-title">{{ 'prepare.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'prepare.subtitle' | translate }}</p>
        </div>

        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else if (submitted()) {
          <!-- Success Message -->
          <div class="preference-card text-center py-5">
            <div class="success-icon">
              <i class="bi bi-check-circle-fill"></i>
            </div>
            <h2>{{ 'prepare.requestSent' | translate }}</h2>
            <p class="text-muted">{{ 'prepare.requestSentMessage' | translate }}</p>
            <div class="d-flex gap-2 justify-content-center mt-4">
              <a routerLink="/" class="btn btn-dark">
                {{ 'common.backToHome' | translate }}
              </a>
              <button class="btn btn-outline-secondary" (click)="resetPage()">
                {{ 'prepare.requestMore' | translate }}
              </button>
            </div>
          </div>
        } @else {
          <!-- Personalized Welcome for Pre-Arrival Guests -->
          @if (bookingInfo()) {
            <div class="preference-section">
              <div class="preference-card welcome-card">
                <div class="preference-item" style="border-bottom: none;">
                  <div class="item-with-icon">
                    <i class="bi bi-person-check item-icon"></i>
                    <div>
                      <label class="preference-label">{{ 'prepare.welcomeGuest' | translate }} {{ bookingInfo()!.guestFirstName }}!</label>
                      <p class="preference-description">{{ 'prepare.preArrivalMessage' | translate }}</p>
                      @if (bookingInfo()!.roomNumber) {
                        <span class="room-badge mt-2">
                          <i class="bi bi-door-open me-1"></i>Room {{ bookingInfo()!.roomNumber }}
                        </span>
                      }
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- Room Number Input (only if no booking and no room set) -->
          @if (!bookingInfo() && !roomNumber()) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-door-open"></i>
                {{ 'prepare.enterRoom' | translate }}
              </h3>
              <div class="preference-card">
                <div class="preference-item" style="border-bottom: none; flex-direction: column; align-items: stretch;">
                  <p class="preference-description mb-3">{{ 'prepare.enterRoomDesc' | translate }}</p>
                  <div class="input-group">
                    <input type="text"
                           class="form-control"
                           [placeholder]="'prepare.roomPlaceholder' | translate"
                           #roomInput>
                    <button class="btn btn-dark" (click)="setRoom(roomInput.value)">
                      {{ 'common.confirm' | translate }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- Services Section (Chargeable) -->
          @if (services().length > 0) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-star"></i>
                {{ 'prepare.enhanceStay' | translate }}
              </h3>
              <div class="preference-card">
                @for (service of services(); track service.id; let last = $last) {
                  <div class="preference-item" [class.selected]="isSelected(service)" [style.border-bottom]="last ? 'none' : null" (click)="toggleService(service)">
                    <div class="item-with-icon">
                      <i class="bi item-icon" [ngClass]="service.icon || 'bi-star'"></i>
                      <div>
                        <label class="preference-label">{{ service.name }}</label>
                        @if (service.description) {
                          <p class="preference-description">{{ service.description }}</p>
                        }
                        @if (service.isChargeable && service.price) {
                          <span class="service-price">
                            {{ service.currency || 'ZAR' }} {{ service.price | number:'1.2-2' }}
                            @if (service.pricingUnit) {
                              <span class="price-unit">/ {{ service.pricingUnit }}</span>
                            }
                          </span>
                        }
                      </div>
                    </div>
                    <div class="select-indicator">
                      @if (isSelected(service)) {
                        <div class="selected-badge">
                          <i class="bi bi-check-circle-fill"></i>
                        </div>
                      } @else {
                        <button class="btn btn-sm request-btn">
                          {{ 'housekeeping.request' | translate }}
                        </button>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Items Section (Free amenities) -->
          @if (items().length > 0) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-box-seam"></i>
                {{ 'prepare.requestItems' | translate }}
              </h3>
              <div class="preference-card">
                @for (item of items(); track item.id; let last = $last) {
                  <div class="preference-item" [class.selected]="isSelected(item)" [style.border-bottom]="last ? 'none' : null" (click)="toggleItem(item)">
                    <div class="item-with-icon">
                      <i class="bi item-icon" [ngClass]="item.icon || 'bi-box-seam'"></i>
                      <div>
                        <label class="preference-label">{{ item.name }}</label>
                        @if (item.estimatedTime) {
                          <p class="preference-description">
                            <i class="bi bi-clock me-1"></i>{{ item.estimatedTime }} min delivery
                          </p>
                        }
                      </div>
                    </div>
                    <div class="select-indicator">
                      @if (isSelected(item)) {
                        <div class="selected-badge">
                          <i class="bi bi-check-circle-fill"></i>
                        </div>
                      } @else {
                        <button class="btn btn-sm request-btn">
                          {{ 'housekeeping.request' | translate }}
                        </button>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- Empty State -->
          @if (services().length === 0 && items().length === 0) {
            <div class="preference-section">
              <div class="preference-card text-center py-5">
                <i class="bi bi-inbox empty-icon"></i>
                <h3 class="mt-3">{{ 'prepare.noItemsTitle' | translate }}</h3>
                <p class="text-muted">{{ 'prepare.noItemsMessage' | translate }}</p>
              </div>
            </div>
          }

          <!-- Submit Button -->
          @if (selectedItems().length > 0 && (roomNumber() || bookingInfo())) {
            <div class="submit-section">
              <div class="selected-summary">
                {{ selectedItems().length }} {{ 'prepare.itemsSelected' | translate }}
              </div>
              <button class="btn btn-dark btn-lg w-100"
                      [disabled]="submitting()"
                      (click)="submitRequests()">
                @if (submitting()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                {{ 'prepare.submitRequest' | translate }}
              </button>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0 6rem;
    }

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

    .preference-section {
      margin-bottom: 1.5rem;
    }

    .section-title {
      font-size: 1.1rem;
      font-weight: 600;
      margin-bottom: 0.75rem;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: white;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .section-title i {
      font-size: 1rem;
    }

    .preference-card {
      background: white;
      border-radius: 12px;
      padding: 1rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }

    .preference-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 0;
      border-bottom: 1px solid #e9ecef;
      cursor: pointer;
      transition: background 0.2s ease;
    }

    .preference-item:first-child {
      padding-top: 0;
    }

    .preference-item:last-child {
      border-bottom: none;
      padding-bottom: 0;
    }

    .preference-item:hover {
      background: #f8f9fa;
      margin: 0 -1rem;
      padding-left: 1rem;
      padding-right: 1rem;
    }

    .preference-item.selected {
      background: #f0fdf4;
      margin: 0 -1rem;
      padding-left: 1rem;
      padding-right: 1rem;
      border-radius: 8px;
    }

    .preference-label {
      font-size: 1rem;
      font-weight: 600;
      margin-bottom: 0.25rem;
      display: block;
      color: #1a1a1a;
    }

    .preference-description {
      font-size: 0.85rem;
      color: #666;
      margin: 0;
    }

    /* Item with Icon Styles */
    .item-with-icon {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex: 1;
      padding-right: 1rem;
    }

    .item-icon {
      font-size: 1.25rem;
      color: #333;
      width: 1.5rem;
      text-align: center;
    }

    /* Request Button Styles */
    .request-btn {
      padding: 0.4rem 1rem;
      border-radius: 50px;
      font-size: 0.85rem;
      font-weight: 500;
      min-width: 85px;
      background-color: #333;
      border-color: #333;
      color: white;
    }

    .request-btn:hover:not(:disabled) {
      background-color: #1a1a1a;
      border-color: #1a1a1a;
      color: white;
    }

    /* Selected Badge */
    .selected-badge {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: #28a745;
      font-weight: 600;
      font-size: 0.85rem;
    }

    .selected-badge i {
      font-size: 1.25rem;
    }

    /* Service Price */
    .service-price {
      font-size: 0.9rem;
      font-weight: 600;
      color: #059669;
      margin-top: 0.25rem;
      display: block;
    }

    .price-unit {
      font-weight: 400;
      color: #666;
    }

    /* Welcome Card */
    .welcome-card {
      background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%);
      border: 1px solid #bbf7d0;
    }

    .welcome-card .item-icon {
      color: #16a34a;
      font-size: 1.5rem;
    }

    .welcome-card .preference-label {
      color: #166534;
    }

    .welcome-card .preference-description {
      color: #15803d;
    }

    .room-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.25rem 0.75rem;
      background: white;
      border-radius: 50px;
      font-size: 0.85rem;
      font-weight: 600;
      color: #166534;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
    }

    /* Input Group */
    .input-group {
      display: flex;
      gap: 0.5rem;
    }

    .form-control {
      flex: 1;
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 0.75rem 1rem;
      font-size: 0.95rem;
      transition: all 0.2s ease;
    }

    .form-control:focus {
      background: white;
      border-color: #333;
      box-shadow: 0 0 0 3px rgba(51, 51, 51, 0.15);
      outline: none;
    }

    /* Empty State */
    .empty-icon {
      font-size: 3.5rem;
      color: #d1d5db;
    }

    /* Success Icon */
    .success-icon {
      font-size: 4rem;
      color: #28a745;
      margin-bottom: 1rem;
    }

    .success-icon i {
      display: block;
    }

    /* Submit Section */
    .submit-section {
      position: fixed;
      bottom: 0;
      left: 0;
      right: 0;
      padding: 1rem;
      background: white;
      border-top: 1px solid #e9ecef;
      box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.1);
      z-index: 100;
    }

    .selected-summary {
      text-align: center;
      font-size: 0.9rem;
      color: #666;
      margin-bottom: 0.75rem;
    }

    /* Dark Button */
    .btn-dark {
      background: #333;
      border-color: #333;
      border-radius: 8px;
      font-weight: 500;
    }

    .btn-dark:hover {
      background: #1a1a1a;
      border-color: #1a1a1a;
    }

    .btn-dark:disabled {
      background: #d1d5db;
      border-color: #d1d5db;
      color: #9ca3af;
    }

    .btn-outline-secondary {
      border-radius: 8px;
      font-weight: 500;
    }

    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .item-icon {
        font-size: 1.1rem;
      }

      .request-btn {
        padding: 0.35rem 0.75rem;
        font-size: 0.8rem;
        min-width: 75px;
      }
    }
  `]
})
export class PrepareComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private roomContextService = inject(RoomContextService);
  private analyticsService = inject(AnalyticsService);
  private route = inject(ActivatedRoute);

  loading = signal(true);
  submitting = signal(false);
  submitted = signal(false);

  items = signal<PrepareItem[]>([]);
  services = signal<PrepareItem[]>([]);
  selectedItems = signal<PrepareItem[]>([]);
  roomNumber = signal<string>('');
  bookingInfo = signal<BookingInfo | null>(null);
  bookingId = signal<number | null>(null);

  ngOnInit(): void {
    // Check for booking or room in URL params
    this.route.queryParams.subscribe(params => {
      // Check for booking ID first (pre-arrival flow)
      if (params['booking']) {
        const bookingIdParam = parseInt(params['booking'], 10);
        if (!isNaN(bookingIdParam)) {
          this.bookingId.set(bookingIdParam);
          this.loadBookingInfo(bookingIdParam);
        }
      } else if (params['room']) {
        // Room number provided (post-check-in flow)
        this.roomNumber.set(params['room']);
        this.roomContextService.setRoomNumber(params['room']);
      } else {
        // Check saved room from context
        this.roomNumber.set(this.roomContextService.getRoomNumber() || '');
      }
    });

    this.loadPrepareItems();
  }

  loadBookingInfo(bookingId: number): void {
    this.apiService.getBookingInfo(bookingId).subscribe({
      next: (booking) => {
        if (booking) {
          this.bookingInfo.set(booking);
          // If booking has a room number (checked in), use it
          if (booking.roomNumber) {
            this.roomNumber.set(booking.roomNumber);
            this.roomContextService.setRoomNumber(booking.roomNumber);
          }
        }
      },
      error: (err) => {
        console.error('Error loading booking info:', err);
      }
    });
  }

  loadPrepareItems(): void {
    this.loading.set(true);
    this.apiService.getPrepareItems().subscribe({
      next: (response) => {
        this.items.set(response.items || []);
        this.services.set(response.services || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  setRoom(room: string): void {
    if (room.trim()) {
      this.roomNumber.set(room.trim());
      this.roomContextService.setRoomNumber(room.trim());
    }
  }

  isSelected(item: PrepareItem): boolean {
    return this.selectedItems().some(i => i.id === item.id && i.type === item.type);
  }

  toggleService(service: PrepareItem): void {
    const current = this.selectedItems();
    const index = current.findIndex(i => i.id === service.id && i.type === 'service');
    if (index >= 0) {
      this.selectedItems.set([...current.slice(0, index), ...current.slice(index + 1)]);
    } else {
      this.selectedItems.set([...current, { ...service, type: 'service' }]);
    }
  }

  toggleItem(item: PrepareItem): void {
    const current = this.selectedItems();
    const index = current.findIndex(i => i.id === item.id && i.type === 'item');
    if (index >= 0) {
      this.selectedItems.set([...current.slice(0, index), ...current.slice(index + 1)]);
    } else {
      this.selectedItems.set([...current, { ...item, type: 'item' }]);
    }
  }

  submitRequests(): void {
    // Allow submission if we have room number OR booking info
    if (this.selectedItems().length === 0) return;
    if (!this.roomNumber() && !this.bookingInfo()) return;

    this.submitting.set(true);
    const selected = this.selectedItems();

    // Get room number - from direct input, booking info, or generate placeholder for pre-arrival
    const room = this.roomNumber() || this.bookingInfo()?.roomNumber || 'Pre-Arrival';
    const guestName = this.bookingInfo()?.guestName;

    // Track analytics
    this.analyticsService.trackEvent('prepare_items_requested', {
      itemCount: selected.length,
      items: selected.map(i => i.name).join(', '),
      isPreArrival: !this.roomNumber() && !!this.bookingInfo()
    });

    // Submit all requests
    let completed = 0;
    const total = selected.length;

    selected.forEach(item => {
      if (item.type === 'service') {
        const request: ServiceRequest = {
          serviceId: item.id,
          roomNumber: room,
          guestName: guestName,
          source: 'prepare_page'
        };
        this.apiService.submitServiceRequest(request).subscribe({
          next: () => {
            completed++;
            if (completed === total) {
              this.submitting.set(false);
              this.submitted.set(true);
            }
          },
          error: () => {
            completed++;
            if (completed === total) {
              this.submitting.set(false);
              this.submitted.set(true);
            }
          }
        });
      } else {
        const request: ItemRequest = {
          requestItemId: item.id,
          roomNumber: room,
          notes: guestName ? `Guest: ${guestName}` : undefined
        };
        this.apiService.submitItemRequest(request).subscribe({
          next: () => {
            completed++;
            if (completed === total) {
              this.submitting.set(false);
              this.submitted.set(true);
            }
          },
          error: () => {
            completed++;
            if (completed === total) {
              this.submitting.set(false);
              this.submitted.set(true);
            }
          }
        });
      }
    });
  }

  resetPage(): void {
    this.submitted.set(false);
    this.selectedItems.set([]);
  }
}
