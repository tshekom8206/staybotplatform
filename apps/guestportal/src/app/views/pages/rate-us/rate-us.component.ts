import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { RoomContextService } from '../../../core/services/room-context.service';
import { GuestApiService } from '../../../core/services/guest-api.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

@Component({
  selector: 'app-rate-us',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, TranslateModule],
  template: `
    <div class="page-container">
      <div class="container">
        <!-- Page Header with Glassmorphism -->
        <div class="page-header">
          <a routerLink="/" class="back-link">
            <i class="bi bi-arrow-left"></i> {{ 'common.back' | translate }}
          </a>
          <h1 class="page-title">{{ 'rateUs.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'rateUs.subtitle' | translate }}</p>
        </div>

        @if (!submitted()) {
          <form (ngSubmit)="submitRating()" class="rating-form">
            <!-- Star Rating -->
            <div class="mb-4 text-center">
              <p class="mb-3">{{ 'rateUs.howWasStay' | translate }}</p>
              <div class="star-rating">
                @for (star of [1,2,3,4,5]; track star) {
                  <button type="button"
                          class="star-btn"
                          [class.active]="rating >= star"
                          (click)="setRating(star)">
                    <i class="bi" [class]="rating >= star ? 'bi-star-fill' : 'bi-star'"></i>
                  </button>
                }
              </div>
              <p class="rating-label mt-2" [ngClass]="getRatingClass()">{{ getRatingLabel() }}</p>
            </div>

            <!-- Guest Name -->
            <div class="mb-3">
              <label class="form-label">{{ 'rateUs.yourName' | translate }}</label>
              <input type="text"
                     class="form-control"
                     [(ngModel)]="guestName"
                     name="guestName"
                     [placeholder]="'rateUs.namePlaceholder' | translate">
            </div>

            <!-- Comment -->
            <div class="mb-4">
              <label class="form-label">{{ 'rateUs.comment' | translate }}</label>
              <textarea class="form-control"
                        [(ngModel)]="comment"
                        name="comment"
                        rows="4"
                        [placeholder]="'rateUs.commentPlaceholder' | translate"></textarea>
            </div>

            <!-- Submit Button -->
            <button type="submit"
                    class="btn btn-primary btn-lg w-100"
                    [disabled]="rating === 0 || submitting()">
              @if (submitting()) {
                <span class="spinner-border spinner-border-sm me-2"></span>
              }
              {{ 'rateUs.submit' | translate }}
            </button>
          </form>
        } @else {
          <!-- Thank You Message -->
          <div class="success-card text-center">
            <div class="success-icon">
              <i class="bi bi-heart-fill"></i>
            </div>
            <h2>{{ 'rateUs.thankYou' | translate }}</h2>
            <p class="text-muted">{{ 'rateUs.thankYouMessage' | translate }}</p>
            <div class="d-flex gap-2 justify-content-center">
              <a routerLink="/" class="btn btn-outline-primary">
                {{ 'common.back' | translate }}
              </a>
              <button class="btn btn-primary" (click)="reset()">
                {{ 'rateUs.rateAnother' | translate }}
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1rem 0; }

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

    .rating-form {
      background: white;
      padding: 1.5rem;
      border-radius: 16px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
    }

    .rating-form > .mb-4.text-center > p {
      color: #555;
      font-weight: 500;
      font-size: 1rem;
    }

    /* Form Labels */
    .form-label {
      display: block;
      color: #333;
      font-weight: 600;
      font-size: 0.9rem;
      margin-bottom: 0.5rem;
    }

    .star-rating {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.5rem 0;
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
      font-size: 2.5rem;
      color: #d4d4d4;
      transition: color 0.15s ease;
    }
    .star-btn.active i {
      color: #fbbf24;
    }

    .rating-label {
      font-size: 0.95rem;
      font-weight: 600;
      min-height: 1.5em;
      color: #888;
      transition: all 0.2s ease;
    }
    .rating-label.poor { color: #ef4444; }
    .rating-label.fair { color: #f97316; }
    .rating-label.good { color: #eab308; }
    .rating-label.very-good { color: #22c55e; }
    .rating-label.excellent { color: #16a34a; }

    /* Form Controls */
    .form-control {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 10px;
      padding: 0.75rem 1rem;
      font-size: 0.95rem;
      transition: all 0.2s ease;
    }
    .form-control:focus {
      background: white;
      border-color: #fbbf24;
      box-shadow: 0 0 0 3px rgba(251, 191, 36, 0.15);
      outline: none;
    }
    .form-control::placeholder {
      color: #9ca3af;
    }

    /* Submit Button */
    .btn-primary {
      background: #1a1a1a;
      border: none;
      border-radius: 10px;
      padding: 0.875rem;
      font-weight: 600;
      font-size: 1rem;
      color: white;
      transition: all 0.2s ease;
    }
    .btn-primary:hover {
      background: #333;
      transform: translateY(-1px);
    }
    .btn-primary:disabled {
      background: #d1d5db;
      color: #9ca3af;
      transform: none;
    }

    .success-card {
      padding: 2.5rem 1.5rem;
      background: white;
      border-radius: 16px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
    }
    .success-icon {
      font-size: 4rem;
      color: #22c55e;
      margin-bottom: 1rem;
    }
    .success-card h2 {
      color: #1a1a1a;
      margin-bottom: 0.5rem;
      font-weight: 700;
    }
    .success-card p {
      color: #6b7280;
    }

    .btn-outline-primary {
      border: 2px solid #1a1a1a;
      color: #1a1a1a;
      background: transparent;
      border-radius: 10px;
      font-weight: 600;
      transition: all 0.2s ease;
    }
    .btn-outline-primary:hover {
      background: #1a1a1a;
      color: white;
    }
  `]
})
export class RateUsComponent {
  private roomContextService = inject(RoomContextService);
  private apiService = inject(GuestApiService);
  private analyticsService = inject(AnalyticsService);

  rating = 0;
  comment = '';
  guestName = '';
  submitted = signal(false);
  submitting = signal(false);

  private ratingLabels = ['', 'Poor', 'Fair', 'Good', 'Very Good', 'Excellent'];

  setRating(value: number): void {
    this.rating = value;
  }

  getRatingLabel(): string {
    return this.ratingLabels[this.rating] || '';
  }

  getRatingClass(): string {
    const classes: Record<number, string> = {
      1: 'poor',
      2: 'fair',
      3: 'good',
      4: 'very-good',
      5: 'excellent'
    };
    return classes[this.rating] || '';
  }

  submitRating(): void {
    if (this.rating === 0) return;

    this.submitting.set(true);

    // Track rating submission in GA4
    this.analyticsService.trackRatingSubmitted(this.rating, !!this.comment);

    this.apiService.submitRating({
      rating: this.rating,
      comment: this.comment || undefined,
      roomNumber: this.roomContextService.getRoomNumber() || undefined,
      guestName: this.guestName || undefined
    }).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
        }
      },
      error: (error) => {
        console.error('Failed to submit rating:', error);
        this.submitting.set(false);
        // Still show success (API might be offline)
        this.submitted.set(true);
      }
    });
  }

  reset(): void {
    this.rating = 0;
    this.comment = '';
    this.guestName = '';
    this.submitted.set(false);
  }
}
