import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, BookingInfo } from '../../../core/services/guest-api.service';
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
        } @else if (checkoutSuccess()) {
          <!-- Success State -->
          <div class="success-container">
            <div class="success-card">
              <i class="bi bi-check-circle"></i>
              <h2>{{ 'checkout.thankYou' | translate }}</h2>
              <p class="success-message">{{ 'checkout.thankYouMessage' | translate }}</p>
              <p class="success-details">{{ 'checkout.thankYouDetails' | translate }}</p>

              <!-- Lost & Found Reminder -->
              <div class="lost-found-reminder">
                <i class="bi bi-box-seam"></i>
                <div class="reminder-content">
                  <h4>{{ 'checkout.lostAndFound' | translate }}</h4>
                  <a routerLink="/lost-and-found" class="btn btn-outline-dark">
                    {{ 'checkout.reportItem' | translate }}
                  </a>
                </div>
              </div>

              <!-- Feedback Link -->
              <div class="feedback-prompt">
                <h4>{{ 'checkout.shareExperience' | translate }}</h4>
                <a routerLink="/feedback" class="btn btn-primary">
                  {{ 'checkout.leaveFeedback' | translate }}
                </a>
              </div>

              <a routerLink="/" class="btn btn-secondary">
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
                  <div class="summary-row">
                    <span class="label">{{ 'checkout.checkoutTime' | translate }}</span>
                    <span class="value">11:00 AM</span>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- Checkout Action -->
          <div class="checkout-action-section">
            <button
              class="btn-checkout"
              (click)="requestCheckout()"
              [disabled]="submitting()"
            >
              @if (submitting()) {
                <span class="spinner"></span>
                {{ 'checkout.submittingRequest' | translate }}
              } @else {
                <i class="bi bi-door-open"></i>
                {{ 'checkout.requestCheckout' | translate }}
              }
            </button>
          </div>
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

    /* Checkout Action */
    .checkout-action-section {
      margin: 2rem 0;
    }

    .btn-checkout {
      width: 100%;
      padding: 1rem 1.5rem;
      background: #333;
      color: white;
      border: none;
      border-radius: 50px;
      font-weight: 600;
      font-size: 1.1rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      transition: all 0.2s ease;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }

    .btn-checkout:hover:not(:disabled) {
      background: #1a1a1a;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    }

    .btn-checkout:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-checkout i {
      font-size: 1.25rem;
    }

    .spinner {
      display: inline-block;
      width: 1.25rem;
      height: 1.25rem;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    /* Success State */
    .success-container {
      padding: 2rem 0;
    }

    .success-card {
      background: white;
      border-radius: 12px;
      padding: 2rem;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      text-align: center;
    }

    .success-card i.bi-check-circle {
      font-size: 4rem;
      color: #16a34a;
      margin-bottom: 1rem;
    }

    .success-card h2 {
      font-size: 1.5rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 0.5rem;
    }

    .success-message {
      font-size: 1.1rem;
      color: #666;
      margin: 0 0 0.5rem;
    }

    .success-details {
      font-size: 0.95rem;
      color: #888;
      margin: 0 0 2rem;
    }

    /* Lost & Found Reminder */
    .lost-found-reminder {
      background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
      border-radius: 12px;
      padding: 1.5rem;
      margin: 2rem 0;
      display: flex;
      align-items: center;
      gap: 1rem;
      color: white;
    }

    .lost-found-reminder i {
      font-size: 2.5rem;
      flex-shrink: 0;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .reminder-content {
      flex: 1;
      text-align: left;
    }

    .reminder-content h4 {
      margin: 0 0 0.75rem;
      font-size: 1.1rem;
      font-weight: 600;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .reminder-content .btn {
      background: rgba(255, 255, 255, 0.2);
      border: 2px solid rgba(255, 255, 255, 0.4);
      color: white;
      padding: 0.5rem 1.25rem;
      font-weight: 500;
      text-shadow: 0 1px 4px rgba(0, 0, 0, 0.3);
    }

    .reminder-content .btn:hover {
      background: rgba(255, 255, 255, 0.3);
      border-color: rgba(255, 255, 255, 0.5);
    }

    /* Feedback Prompt */
    .feedback-prompt {
      margin: 2rem 0;
    }

    .feedback-prompt h4 {
      font-size: 1.1rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 1rem;
    }

    .btn-primary {
      background: #333;
      border-color: #333;
      color: white;
      padding: 0.75rem 1.5rem;
      border-radius: 50px;
      font-weight: 500;
      border: none;
      cursor: pointer;
      transition: all 0.2s ease;
      width: 100%;
    }

    .btn-primary:hover {
      background: #1a1a1a;
      transform: translateY(-1px);
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }

    .btn-secondary {
      background: #f8f9fa;
      border-color: #f8f9fa;
      color: #666;
      padding: 0.75rem 1.5rem;
      border-radius: 50px;
      font-weight: 500;
      border: none;
      cursor: pointer;
      transition: all 0.2s ease;
      margin-top: 1rem;
      width: 100%;
      text-decoration: none;
      display: inline-block;
    }

    .btn-secondary:hover {
      background: #e9ecef;
      color: #666;
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

    /* Mobile Adjustments */
    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .btn-checkout {
        font-size: 1rem;
      }

      .lost-found-reminder {
        flex-direction: column;
        text-align: center;
      }

      .reminder-content {
        text-align: center;
      }
    }
  `]
})
export class CheckoutComponent implements OnInit {
  // State management with signals
  bookingInfo = signal<BookingInfo | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  checkoutSuccess = signal(false);
  submitting = signal(false);

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
          this.loading.set(false);
        } else {
          this.error.set('Booking not found');
          this.loading.set(false);
        }
      },
      error: (err) => {
        console.error('Error loading booking info:', err);
        this.error.set('Unable to load booking information');
        this.loading.set(false);
      }
    });
  }

  requestCheckout(): void {
    const booking = this.bookingInfo();
    if (!booking || !booking.roomNumber) {
      this.error.set('Room number is required');
      return;
    }

    this.submitting.set(true);

    // Use the existing custom request endpoint
    const checkoutRequest = {
      description: `Checkout request for Room ${booking.roomNumber}`,
      roomNumber: booking.roomNumber,
      department: 'FrontDesk',
      source: 'checkout_page'
    };

    this.apiService.submitCustomRequest(checkoutRequest).subscribe({
      next: () => {
        this.submitting.set(false);
        this.checkoutSuccess.set(true);
      },
      error: (err) => {
        console.error('Error submitting checkout request:', err);
        this.error.set('Unable to submit checkout request. Please try again.');
        this.submitting.set(false);
      }
    });
  }
}
