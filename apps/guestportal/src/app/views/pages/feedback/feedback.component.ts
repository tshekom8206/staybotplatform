import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, FeedbackCategory, QuickFeedbackRequest, BookingInfo } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'feedback.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'feedback.subtitle' | translate }}</p>
        </div>

        @if (!submitted()) {
          <!-- Rating Section -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-star"></i>
              {{ 'feedback.howIsStay' | translate }}
            </h3>
            <div class="preference-card">
              <div class="star-rating">
                @for (star of [1,2,3,4,5]; track star) {
                  <button type="button"
                          class="star-btn"
                          [class.active]="rating() >= star"
                          (click)="setRating(star)">
                    <i class="bi" [class]="rating() >= star ? 'bi-star-fill' : 'bi-star'"></i>
                  </button>
                }
              </div>
              @if (rating() > 0) {
                <p class="rating-label" [ngClass]="getRatingClass()">{{ getRatingLabel() }}</p>
              }
            </div>
          </div>

          <!-- Issue Categories (shown for ratings <= 3) -->
          @if (rating() > 0 && rating() <= 3) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-exclamation-circle"></i>
                {{ 'feedback.whatCanWeImprove' | translate }}
              </h3>
              <div class="preference-card">
                <p class="section-hint">{{ 'feedback.selectIssue' | translate }}</p>
                <div class="category-grid">
                  @for (cat of categories(); track cat.id) {
                    <button class="category-btn"
                            [class.selected]="selectedCategory() === cat.id"
                            (click)="selectCategory(cat.id)">
                      <i class="bi" [ngClass]="cat.icon"></i>
                      <span>{{ cat.name }}</span>
                    </button>
                  }
                </div>
              </div>
            </div>
          }

          <!-- Comment Section -->
          <div class="preference-section">
            <h3 class="section-title">
              <i class="bi bi-chat-text"></i>
              {{ rating() > 3 ? ('feedback.shareExperience' | translate) : ('feedback.tellUsMore' | translate) }}
            </h3>
            <div class="preference-card">
              <textarea class="form-control"
                        [(ngModel)]="comment"
                        rows="4"
                        [placeholder]="'feedback.commentPlaceholder' | translate"></textarea>
            </div>
          </div>

          <!-- Room Number (if not set) -->
          @if (!roomNumber()) {
            <div class="preference-section">
              <h3 class="section-title">
                <i class="bi bi-door-open"></i>
                {{ 'feedback.yourRoom' | translate }}
              </h3>
              <div class="preference-card">
                <input type="text"
                       class="form-control"
                       [(ngModel)]="manualRoom"
                       [placeholder]="'feedback.roomPlaceholder' | translate">
              </div>
            </div>
          }

          <!-- Submit Button -->
          <div class="preference-section">
            <button class="btn btn-dark btn-lg w-100"
                    [disabled]="rating() === 0 || submitting()"
                    (click)="submitFeedback()">
              @if (submitting()) {
                <span class="spinner-border spinner-border-sm me-2"></span>
              }
              {{ 'feedback.submit' | translate }}
            </button>
          </div>
        } @else {
          <!-- Success Message -->
          <div class="preference-section">
            <div class="preference-card text-center py-5">
              <div class="success-icon">
                @if (rating() >= 4) {
                  <i class="bi bi-heart-fill text-pink"></i>
                } @else {
                  <i class="bi bi-check-circle-fill text-success"></i>
                }
              </div>
              <h2>{{ 'feedback.thankYou' | translate }}</h2>
              <p class="text-muted">
                @if (rating() >= 4) {
                  {{ 'feedback.thankYouPositive' | translate }}
                } @else {
                  {{ 'feedback.thankYouNegative' | translate }}
                }
              </p>
              <a routerLink="/" class="btn btn-dark mt-3">
                {{ 'common.backToHome' | translate }}
              </a>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 1rem 0;
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

    .section-hint {
      font-size: 0.875rem;
      color: #666;
      margin-bottom: 1rem;
    }

    /* Rating Section */
    .star-rating {
      display: flex;
      justify-content: center;
      gap: 0.75rem;
      padding: 0.75rem 0;
    }

    .star-btn {
      background: none;
      border: none;
      padding: 0.25rem;
      cursor: pointer;
      transition: transform 0.15s ease;
    }

    .star-btn:hover {
      transform: scale(1.15);
    }

    .star-btn i {
      font-size: 2.75rem;
      color: #e9ecef;
      transition: color 0.15s ease;
    }

    .star-btn.active i {
      color: #333;
    }

    .rating-label {
      font-size: 1rem;
      font-weight: 600;
      margin-top: 0.5rem;
      text-align: center;
      transition: all 0.2s ease;
    }

    .rating-label.poor { color: #dc3545; }
    .rating-label.fair { color: #fd7e14; }
    .rating-label.okay { color: #ffc107; }
    .rating-label.good { color: #28a745; }
    .rating-label.excellent { color: #28a745; }

    /* Issue Categories */
    .category-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.75rem;
    }

    .category-btn {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 1rem 0.5rem;
      background: #f8f9fa;
      border: 2px solid transparent;
      border-radius: 12px;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .category-btn:hover {
      background: #e9ecef;
      border-color: #333;
    }

    .category-btn.selected {
      border-color: #333;
      background: #f0f0f0;
    }

    .category-btn i {
      font-size: 1.5rem;
      color: #666;
    }

    .category-btn.selected i {
      color: #333;
    }

    .category-btn span {
      font-size: 0.85rem;
      font-weight: 500;
      color: #333;
      text-align: center;
    }

    /* Form Controls */
    .form-control {
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

    .form-control::placeholder {
      color: #9ca3af;
    }

    /* Dark Button */
    .btn-dark {
      background: #333;
      border-color: #333;
      border-radius: 8px;
      font-weight: 500;
      padding: 0.875rem;
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

    /* Success Icon */
    .success-icon {
      font-size: 4rem;
      margin-bottom: 1rem;
    }

    .success-icon i {
      display: block;
    }

    .text-pink {
      color: #ec4899;
    }

    .text-success {
      color: #28a745;
    }

    h2 {
      color: #1a1a1a;
      margin-bottom: 0.5rem;
      font-weight: 700;
    }

    @media (max-width: 768px) {
      .page-title {
        font-size: 1.5rem;
      }

      .star-btn i {
        font-size: 2.25rem;
      }
    }

    @media (max-width: 380px) {
      .category-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class FeedbackComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private roomContextService = inject(RoomContextService);
  private analyticsService = inject(AnalyticsService);
  private route = inject(ActivatedRoute);

  rating = signal(0);
  comment = '';
  manualRoom = '';
  selectedCategory = signal<string>('');
  categories = signal<FeedbackCategory[]>([]);

  submitting = signal(false);
  submitted = signal(false);
  roomNumber = signal<string>('');
  bookingInfo = signal<BookingInfo | null>(null);

  private ratingLabels = ['', 'Poor', 'Fair', 'Okay', 'Good', 'Excellent'];

  ngOnInit(): void {
    // Check for booking or room in URL params
    this.route.queryParams.subscribe(params => {
      // Check for booking ID first
      if (params['booking']) {
        const bookingId = parseInt(params['booking'], 10);
        if (!isNaN(bookingId)) {
          this.loadBookingInfo(bookingId);
        }
      } else if (params['room']) {
        this.roomNumber.set(params['room']);
        this.roomContextService.setRoomNumber(params['room']);
      } else {
        this.roomNumber.set(this.roomContextService.getRoomNumber() || '');
      }
    });

    this.loadCategories();
  }

  loadBookingInfo(bookingId: number): void {
    this.apiService.getBookingInfo(bookingId).subscribe({
      next: (booking) => {
        if (booking) {
          this.bookingInfo.set(booking);
          if (booking.roomNumber) {
            this.roomNumber.set(booking.roomNumber);
            this.roomContextService.setRoomNumber(booking.roomNumber);
          }
        }
      }
    });
  }

  loadCategories(): void {
    this.apiService.getFeedbackCategories().subscribe({
      next: (response) => {
        this.categories.set(response.categories || []);
      }
    });
  }

  setRating(value: number): void {
    this.rating.set(value);
    // Clear issue category if rating improves
    if (value > 3) {
      this.selectedCategory.set('');
    }
  }

  getRatingLabel(): string {
    return this.ratingLabels[this.rating()] || '';
  }

  getRatingClass(): string {
    const classes: Record<number, string> = {
      1: 'poor',
      2: 'fair',
      3: 'okay',
      4: 'good',
      5: 'excellent'
    };
    return classes[this.rating()] || '';
  }

  selectCategory(categoryId: string): void {
    if (this.selectedCategory() === categoryId) {
      this.selectedCategory.set('');
    } else {
      this.selectedCategory.set(categoryId);
    }
  }

  submitFeedback(): void {
    if (this.rating() === 0) return;

    this.submitting.set(true);

    const room = this.roomNumber() || this.manualRoom.trim();

    // Track analytics
    this.analyticsService.trackEvent('quick_feedback_submitted', {
      rating: this.rating(),
      hasComment: !!this.comment.trim(),
      hasIssue: !!this.selectedCategory()
    });

    const request: QuickFeedbackRequest = {
      rating: this.rating(),
      comment: this.comment.trim() || undefined,
      roomNumber: room || undefined,
      issueCategory: this.selectedCategory() || undefined
    };

    this.apiService.submitQuickFeedback(request).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
        }
      },
      error: () => {
        this.submitting.set(false);
        // Still show success for better UX
        this.submitted.set(true);
      }
    });
  }
}
