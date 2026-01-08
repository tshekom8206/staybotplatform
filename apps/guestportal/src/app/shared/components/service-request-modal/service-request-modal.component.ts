import { Component, inject, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, FeaturedService, ServiceRequest } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

@Component({
  selector: 'app-service-request-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  template: `
    @if (isOpen) {
      <div class="modal-backdrop" (click)="close()"></div>
      <div class="modal-container">
        <div class="modal-content">
          <button class="close-btn" (click)="close()">
            <i class="bi bi-x-lg"></i>
          </button>

          @if (!submitted()) {
            <!-- Service Header with Icon -->
            <div class="service-header">
              <div class="service-icon-wrapper">
                <i class="bi" [ngClass]="getIconClass(service?.icon, service?.category)"></i>
              </div>
              <div class="service-info">
                <h3 class="service-name">{{ service?.name }}</h3>
                @if (service?.price) {
                  <div class="service-price">{{ service?.price }}</div>
                }
              </div>
            </div>

            <div class="divider"></div>

            <div class="modal-body">
              <!-- Room Number -->
              <div class="form-group">
                <label>
                  <i class="bi bi-door-open"></i>
                  {{ 'common.roomNumber' | translate }}
                </label>
                <input
                  type="text"
                  [(ngModel)]="formData.roomNumber"
                  [placeholder]="'common.enterRoomNumber' | translate"
                  class="form-control"
                />
              </div>

              <!-- Preferred Time -->
              <div class="form-group">
                <label>
                  <i class="bi bi-clock"></i>
                  {{ 'services.preferredTime' | translate }}
                </label>
                <div class="time-options">
                  <button
                    type="button"
                    class="time-option"
                    [class.selected]="formData.preferredTime === 'asap'"
                    (click)="formData.preferredTime = 'asap'"
                  >
                    <i class="bi bi-lightning-charge"></i>
                    <span>{{ 'services.asap' | translate }}</span>
                  </button>
                  <button
                    type="button"
                    class="time-option"
                    [class.selected]="formData.preferredTime === 'this_afternoon'"
                    (click)="formData.preferredTime = 'this_afternoon'"
                  >
                    <i class="bi bi-sun"></i>
                    <span>{{ 'services.thisAfternoon' | translate }}</span>
                  </button>
                  <button
                    type="button"
                    class="time-option"
                    [class.selected]="formData.preferredTime === 'this_evening'"
                    (click)="formData.preferredTime = 'this_evening'"
                  >
                    <i class="bi bi-moon-stars"></i>
                    <span>{{ 'services.thisEvening' | translate }}</span>
                  </button>
                  <button
                    type="button"
                    class="time-option"
                    [class.selected]="formData.preferredTime === 'tomorrow'"
                    (click)="formData.preferredTime = 'tomorrow'"
                  >
                    <i class="bi bi-calendar-event"></i>
                    <span>{{ 'services.tomorrow' | translate }}</span>
                  </button>
                </div>
              </div>

              <!-- Special Requests -->
              <div class="form-group">
                <label>
                  <i class="bi bi-chat-text"></i>
                  {{ 'services.specialRequests' | translate }}
                </label>
                <textarea
                  [(ngModel)]="formData.specialRequests"
                  class="form-control"
                  rows="3"
                  placeholder="Any special requirements or preferences..."
                ></textarea>
              </div>
            </div>

            <div class="modal-footer">
              <button
                class="submit-btn"
                [disabled]="submitting() || !formData.roomNumber"
                (click)="submitRequest()"
              >
                @if (submitting()) {
                  <span class="spinner"></span>
                  <span>Processing...</span>
                } @else {
                  <i class="bi bi-send-fill"></i>
                  <span>{{ 'services.sendRequest' | translate }}</span>
                }
              </button>
            </div>
          } @else {
            <!-- Success State -->
            <div class="success-content">
              <div class="success-animation">
                <div class="success-circle">
                  <i class="bi bi-check-lg"></i>
                </div>
              </div>
              <h3>{{ 'services.requestSent' | translate }}</h3>
              <p>{{ 'services.requestSentMessage' | translate }}</p>
              <button class="done-btn" (click)="close()">
                <i class="bi bi-check2"></i>
                Done
              </button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(8px);
      -webkit-backdrop-filter: blur(8px);
      z-index: 1000;
    }

    .modal-container {
      position: fixed;
      left: 50%;
      bottom: 0;
      transform: translateX(-50%);
      width: 100%;
      max-width: 420px;
      z-index: 1001;
      animation: slideUp 0.35s cubic-bezier(0.32, 0.72, 0, 1);
    }

    @keyframes slideUp {
      from {
        transform: translateX(-50%) translateY(100%);
      }
      to {
        transform: translateX(-50%) translateY(0);
      }
    }

    .modal-content {
      background: linear-gradient(180deg, #2a2a2a 0%, #1a1a1a 100%);
      border-radius: 28px 28px 0 0;
      padding: 1.75rem 1.5rem 2rem;
      position: relative;
      max-height: 90vh;
      overflow-y: auto;
      box-shadow: 0 -10px 40px rgba(0, 0, 0, 0.4);
    }

    .close-btn {
      position: absolute;
      top: 1rem;
      right: 1rem;
      background: rgba(255, 255, 255, 0.08);
      border: none;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      color: rgba(255, 255, 255, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s;
      font-size: 0.9rem;
    }

    .close-btn:hover {
      background: rgba(255, 255, 255, 0.15);
      color: white;
    }

    /* Service Header */
    .service-header {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding-right: 2.5rem;
    }

    .service-icon-wrapper {
      width: 56px;
      height: 56px;
      border-radius: 16px;
      background: linear-gradient(135deg, rgba(255, 255, 255, 0.15) 0%, rgba(255, 255, 255, 0.05) 100%);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      border: 1px solid rgba(255, 255, 255, 0.1);
    }

    .service-icon-wrapper i {
      font-size: 1.5rem;
      color: white;
    }

    .service-info {
      flex: 1;
      min-width: 0;
    }

    .service-name {
      color: white;
      font-size: 1.15rem;
      font-weight: 600;
      margin: 0 0 0.25rem;
      line-height: 1.3;
    }

    .service-price {
      display: inline-block;
      background: linear-gradient(135deg, #4CAF50 0%, #2E7D32 100%);
      color: white;
      font-size: 0.8rem;
      font-weight: 600;
      padding: 0.25rem 0.75rem;
      border-radius: 20px;
    }

    .divider {
      height: 1px;
      background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.1), transparent);
      margin: 1.25rem 0;
    }

    .modal-body {
      margin-bottom: 1rem;
    }

    .form-group {
      margin-bottom: 1.25rem;
    }

    .form-group label {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: rgba(255, 255, 255, 0.7);
      font-size: 0.85rem;
      font-weight: 500;
      margin-bottom: 0.625rem;
    }

    .form-group label i {
      font-size: 0.9rem;
      opacity: 0.7;
    }

    .form-control {
      width: 100%;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 14px;
      padding: 0.875rem 1rem;
      color: white;
      font-size: 1rem;
      transition: all 0.2s;
    }

    .form-control::placeholder {
      color: rgba(255, 255, 255, 0.3);
    }

    .form-control:focus {
      outline: none;
      border-color: rgba(255, 255, 255, 0.25);
      background: rgba(255, 255, 255, 0.1);
      box-shadow: 0 0 0 3px rgba(255, 255, 255, 0.05);
    }

    textarea.form-control {
      resize: none;
      min-height: 80px;
    }

    /* Time Options */
    .time-options {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.625rem;
    }

    .time-option {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 14px;
      padding: 0.875rem 0.5rem;
      color: rgba(255, 255, 255, 0.8);
      font-size: 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }

    .time-option i {
      font-size: 1.1rem;
      opacity: 0.7;
    }

    .time-option span {
      text-align: center;
      line-height: 1.2;
    }

    .time-option:hover {
      background: rgba(255, 255, 255, 0.1);
      border-color: rgba(255, 255, 255, 0.2);
    }

    .time-option.selected {
      background: white;
      color: #1a1a1a;
      border-color: white;
      font-weight: 600;
    }

    .time-option.selected i {
      opacity: 1;
    }

    /* Submit Button */
    .modal-footer {
      padding-top: 0.5rem;
    }

    .submit-btn {
      width: 100%;
      background: linear-gradient(135deg, #ffffff 0%, #f0f0f0 100%);
      color: #1a1a1a;
      border: none;
      border-radius: 14px;
      padding: 1rem 1.5rem;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.625rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .submit-btn i {
      font-size: 1rem;
    }

    .submit-btn:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(0, 0, 0, 0.2);
    }

    .submit-btn:active:not(:disabled) {
      transform: translateY(0);
    }

    .submit-btn:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }

    .spinner {
      width: 18px;
      height: 18px;
      border: 2px solid rgba(0, 0, 0, 0.1);
      border-top-color: #1a1a1a;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    /* Success State */
    .success-content {
      text-align: center;
      padding: 2rem 1rem;
    }

    .success-animation {
      margin-bottom: 1.5rem;
    }

    .success-circle {
      width: 80px;
      height: 80px;
      border-radius: 50%;
      background: linear-gradient(135deg, #4CAF50 0%, #2E7D32 100%);
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto;
      animation: scaleIn 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
    }

    .success-circle i {
      font-size: 2.5rem;
      color: white;
    }

    @keyframes scaleIn {
      from {
        transform: scale(0);
        opacity: 0;
      }
      to {
        transform: scale(1);
        opacity: 1;
      }
    }

    .success-content h3 {
      color: white;
      font-size: 1.5rem;
      font-weight: 600;
      margin: 0 0 0.5rem;
    }

    .success-content p {
      color: rgba(255, 255, 255, 0.6);
      margin: 0 0 1.75rem;
      font-size: 0.95rem;
      line-height: 1.5;
    }

    .done-btn {
      background: rgba(255, 255, 255, 0.1);
      color: white;
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 12px;
      padding: 0.875rem 2rem;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
    }

    .done-btn:hover {
      background: rgba(255, 255, 255, 0.15);
      border-color: rgba(255, 255, 255, 0.3);
    }

    /* Desktop Styles */
    @media (min-width: 576px) {
      .modal-container {
        bottom: auto;
        top: 50%;
        transform: translate(-50%, -50%);
        animation: fadeIn 0.3s ease;
      }

      @keyframes fadeIn {
        from {
          opacity: 0;
          transform: translate(-50%, -50%) scale(0.95);
        }
        to {
          opacity: 1;
          transform: translate(-50%, -50%) scale(1);
        }
      }

      .modal-content {
        border-radius: 24px;
        padding: 2rem;
      }

      .time-option {
        flex-direction: row;
        gap: 0.5rem;
        padding: 0.875rem 1rem;
      }

      .time-option span {
        text-align: left;
      }
    }
  `]
})
export class ServiceRequestModalComponent {
  private apiService = inject(GuestApiService);
  private roomContext = inject(RoomContextService);
  private analyticsService = inject(AnalyticsService);

  @Input() isOpen = false;
  @Input() service: FeaturedService | null = null;
  @Input() source: string | null = null; // For upsell tracking: weather_warm, weather_hot, featured_carousel, etc.
  @Output() closed = new EventEmitter<void>();

  submitting = signal(false);
  submitted = signal(false);
  private formStartTracked = false;

  formData: ServiceRequest = {
    serviceId: 0,
    roomNumber: '',
    preferredTime: 'asap',
    specialRequests: '',
    source: undefined
  };

  ngOnInit(): void {
    const room = this.roomContext.getRoomNumber();
    if (room) {
      this.formData.roomNumber = room;
    }
  }

  ngOnChanges(): void {
    if (this.service && this.isOpen) {
      this.formData.serviceId = this.service.id;
      this.submitted.set(false);
      const room = this.roomContext.getRoomNumber();
      if (room) {
        this.formData.roomNumber = room;
      }
      // Track form_start when modal opens
      if (!this.formStartTracked) {
        this.trackFormStart();
        this.formStartTracked = true;
      }
    } else if (!this.isOpen) {
      // Reset tracking flag when modal closes
      this.formStartTracked = false;
    }
  }

  private trackFormStart(): void {
    this.analyticsService.trackEvent('form_start', {
      form_name: 'service_request',
      service_name: this.service?.name,
      service_category: this.service?.category,
      page_location: window.location.href
    });
  }

  getIconClass(icon: string | undefined, category: string | undefined): string {
    const iconMap: Record<string, string> = {
      'spa': 'bi-flower1',
      'wellness': 'bi-flower1',
      'massage': 'bi-flower2',
      'restaurant': 'bi-cup-hot',
      'dining': 'bi-cup-hot',
      'car': 'bi-car-front',
      'shuttle': 'bi-bus-front',
      'transport': 'bi-car-front',
      'tour': 'bi-compass',
      'pool': 'bi-water',
      'gym': 'bi-heart-pulse',
      'gift': 'bi-gift'
    };

    const categoryMap: Record<string, string> = {
      'wellness': 'bi-flower1',
      'spa': 'bi-flower1',
      'dining': 'bi-cup-hot',
      'transport': 'bi-car-front',
      'local tours': 'bi-compass'
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

    return 'bi-gift';
  }

  submitRequest(): void {
    if (!this.formData.roomNumber || !this.service) return;

    this.submitting.set(true);
    this.formData.serviceId = this.service.id;
    this.formData.source = this.source || undefined; // Include source for upsell tracking

    this.apiService.submitServiceRequest(this.formData).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
          // Track service request submission
          this.analyticsService.trackServiceRequest(this.service?.name || 'unknown', this.service?.id);
        }
      },
      error: () => {
        this.submitting.set(false);
      }
    });
  }

  close(): void {
    this.isOpen = false;
    this.submitted.set(false);
    this.formData = {
      serviceId: 0,
      roomNumber: this.roomContext.getRoomNumber() || '',
      preferredTime: 'asap',
      specialRequests: ''
    };
    this.closed.emit();
  }
}
