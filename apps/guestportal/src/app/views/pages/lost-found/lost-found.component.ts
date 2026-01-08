import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { GuestApiService, LostItemRequest } from '../../../core/services/guest-api.service';
import { RoomContextService } from '../../../core/services/room-context.service';
import { AnalyticsService } from '../../../core/services/analytics.service';

@Component({
  selector: 'app-lost-found',
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
          <h1 class="page-title">{{ 'lostFound.title' | translate }}</h1>
          <p class="page-subtitle">{{ 'lostFound.reportDescription' | translate }}</p>
        </div>

        @if (true) {
          <!-- Report Lost Item Form -->
          @if (submitted()) {
            <div class="success-message">
              <i class="bi bi-check-circle-fill"></i>
              <h3>{{ 'lostFound.reportSubmitted' | translate }}</h3>
              <p>{{ 'lostFound.reportSubmittedMessage' | translate }}</p>
              <button class="btn btn-primary" (click)="resetForm()">
                {{ 'lostFound.reportAnother' | translate }}
              </button>
            </div>
          } @else {
            <form (ngSubmit)="submitReport()" class="lost-item-form">
              <!-- Item Name -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.itemName' | translate }} *</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.itemName"
                  name="itemName"
                  required
                  placeholder="{{ 'lostFound.itemNamePlaceholder' | translate }}">
              </div>

              <!-- Category -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.category' | translate }}</label>
                <select class="form-select" [(ngModel)]="formData.category" name="category">
                  <option value="">{{ 'lostFound.selectCategory' | translate }}</option>
                  <option value="Electronics">{{ 'lostFound.categoryElectronics' | translate }}</option>
                  <option value="Jewelry">{{ 'lostFound.categoryJewelry' | translate }}</option>
                  <option value="Clothing">{{ 'lostFound.categoryClothing' | translate }}</option>
                  <option value="Documents">{{ 'lostFound.categoryDocuments' | translate }}</option>
                  <option value="Bags">{{ 'lostFound.categoryBags' | translate }}</option>
                  <option value="Keys">{{ 'lostFound.categoryKeys' | translate }}</option>
                  <option value="Other">{{ 'lostFound.categoryOther' | translate }}</option>
                </select>
              </div>

              <!-- Description -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.description' | translate }}</label>
                <textarea
                  class="form-control"
                  [(ngModel)]="formData.description"
                  name="description"
                  rows="3"
                  placeholder="{{ 'lostFound.descriptionPlaceholder' | translate }}"></textarea>
              </div>

              <!-- Color & Brand -->
              <div class="row">
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.color' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.color"
                    name="color"
                    placeholder="{{ 'lostFound.colorPlaceholder' | translate }}">
                </div>
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.brand' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.brand"
                    name="brand"
                    placeholder="{{ 'lostFound.brandPlaceholder' | translate }}">
                </div>
              </div>

              <!-- Location Lost -->
              <div class="mb-3">
                <label class="form-label">{{ 'lostFound.locationLost' | translate }}</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.locationLost"
                  name="locationLost"
                  placeholder="{{ 'lostFound.locationPlaceholder' | translate }}">
              </div>

              <!-- Contact Info -->
              <div class="row">
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.yourName' | translate }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [(ngModel)]="formData.guestName"
                    name="guestName"
                    placeholder="{{ 'lostFound.namePlaceholder' | translate }}">
                </div>
                <div class="col-6 mb-3">
                  <label class="form-label">{{ 'lostFound.phone' | translate }}</label>
                  <input
                    type="tel"
                    class="form-control"
                    [(ngModel)]="formData.phone"
                    name="phone"
                    placeholder="{{ 'lostFound.phonePlaceholder' | translate }}">
                </div>
              </div>

              <!-- Room Number -->
              <div class="mb-4">
                <label class="form-label">{{ 'lostFound.roomNumber' | translate }}</label>
                <input
                  type="text"
                  class="form-control"
                  [(ngModel)]="formData.roomNumber"
                  name="roomNumber"
                  placeholder="{{ 'lostFound.roomPlaceholder' | translate }}">
              </div>

              <button
                type="submit"
                class="btn btn-primary btn-lg w-100"
                [disabled]="submitting() || !formData.itemName">
                @if (submitting()) {
                  <span class="spinner-border spinner-border-sm me-2"></span>
                }
                {{ 'lostFound.submitReport' | translate }}
              </button>
            </form>
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

    .success-message {
      text-align: center;
      padding: 3rem 1.5rem;
      background: #e8f5e9;
      border-radius: 16px;
    }
    .success-message i {
      font-size: 4rem;
      color: #27ae60;
      margin-bottom: 1rem;
    }
    .success-message h3 { margin-bottom: 0.5rem; color: #27ae60; }
    .success-message p { color: #666; margin-bottom: 1.5rem; }

    .lost-item-form {
      background: white;
      padding: 1.5rem;
      border-radius: 16px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }
  `]
})
export class LostFoundComponent implements OnInit {
  private apiService = inject(GuestApiService);
  private roomContext = inject(RoomContextService);
  private analyticsService = inject(AnalyticsService);

  submitting = signal(false);
  submitted = signal(false);

  formData: LostItemRequest = {
    itemName: '',
    category: '',
    description: '',
    color: '',
    brand: '',
    locationLost: '',
    guestName: '',
    phone: '',
    roomNumber: ''
  };

  ngOnInit(): void {
    // Pre-fill room number from context
    const room = this.roomContext.getRoomNumber();
    if (room) {
      this.formData.roomNumber = room;
    }
  }

  submitReport(): void {
    if (!this.formData.itemName) return;

    this.submitting.set(true);

    // Track lost item report in GA4
    this.analyticsService.trackLostItemReport(this.formData.category || 'uncategorized');

    this.apiService.reportLostItem(this.formData).subscribe({
      next: (response) => {
        this.submitting.set(false);
        if (response.success) {
          this.submitted.set(true);
        }
      },
      error: (error) => {
        console.error('Failed to submit report:', error);
        this.submitting.set(false);
      }
    });
  }

  resetForm(): void {
    this.formData = {
      itemName: '',
      category: '',
      description: '',
      color: '',
      brand: '',
      locationLost: '',
      guestName: '',
      phone: '',
      roomNumber: this.roomContext.getRoomNumber() || ''
    };
    this.submitted.set(false);
  }
}
